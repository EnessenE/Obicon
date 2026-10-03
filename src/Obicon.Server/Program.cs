using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Obicon.Server.BackgroundServices;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.Health;
using Obicon.Server.Metrics;
using Obicon.Server.Middleware;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);

    // Default when appsettings.json defines no sinks: plain console, invariant culture
    if (!context.Configuration.GetSection("Serilog:WriteTo").GetChildren().Any())
    {
        configuration.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
    }
});

// Optional OTLP egress for metrics and logs: absent or empty means scrape-only.
// Set "Otlp:Endpoint" (e.g. "http:// collector:4317") to push both streams to the
// user's observability backend; the Prometheus scrape endpoint stays up either way.
var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
var hasOtlpEndpoint = !string.IsNullOrWhiteSpace(otlpEndpoint);

builder.Services.AddOpenTelemetry()
    .WithMetrics(b =>
    {
        b.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter(ServerMetrics.ServerMeterName)
            .AddMeter(ServerMetrics.TestsMeterName)
            .AddPrometheusExporter();

        if (hasOtlpEndpoint)
        {
            b.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint!));
        }
    })
    .WithLogging(b =>
    {
        // The node log funnel writes through the OTel logger provider, so shipped node
        // entries leave through the same endpoint as the metrics
        if (hasOtlpEndpoint)
        {
            b.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint!));
        }
    });

builder.Services.AddCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
var authHeader = builder.Configuration["ServerSettings:AuthHeader"] ?? new ServerSettings().AuthHeader;
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Obicon API", Version = "v1" });
    c.AddSecurityDefinition("Authorization", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = $"Enter '{authHeader}' for authorization"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Authorization" } }, Array.Empty<string>() }
    });
});
builder.Services.AddSingleton<INodeService, NodeService>();
builder.Services.AddSingleton<INodePoolService, NodePoolService>();
builder.Services.AddSingleton<ITestService, TestService>();
builder.Services.AddSingleton<ITestQueueService, TestQueueService>();
builder.Services.AddSingleton<NodeConnectionManager>();
builder.Services.AddSingleton<IConfigRepository, JsonConfigRepository>();
builder.Services.AddDbContextFactory<ObiconDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// The ServerSettings section is the configuration layer of the settings system:
// values present there (or as ServerSettings__* environment variables) are forced and read-only
builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));
builder.Services.AddSingleton<IServerSettingsService, ServerSettingsService>();
builder.Services.AddSingleton<Obicon.Server.Metrics.ITestMetricsEmitter, Obicon.Server.Metrics.TestMetricsEmitter>();
builder.Services.AddSingleton<Obicon.Server.Services.INodeLogFunnel, Obicon.Server.Services.NodeLogFunnel>();
builder.Services.AddSingleton<NodePolicyBroadcaster>();
builder.Services.AddSingleton<IEnrollTokenService, EnrollTokenService>();
builder.Services.AddSingleton<INodeEnrollmentService, NodeEnrollmentService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddHostedService<TestQueueProcessor>();
builder.Services.AddHostedService<TestScheduler>();
builder.Services.AddHostedService<Obicon.Server.Metrics.MetricsSampler>();
builder.Services.AddHostedService<Obicon.Server.WebSockets.ConnectionWatcher>();

var app = builder.Build();

// Apply EF Core migrations on startup: a fresh database is created, and an existing
// one is brought up to the current schema. There is no upgrade path from the
// pre-0.5.0 SQLite database - the 0.5.0 release is a clean, breaking cut.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ObiconDbContext>();
    await db.Database.MigrateAsync();
}

// Enable CORS for frontend on port 5003
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseSwagger();
app.UseSwaggerUI();

app.UseWebSockets();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<WebSocketMiddleware>();

app.MapControllers();
app.MapHealthChecks("/v1/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.ToString(),
            Entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new { Status = e.Value.Status.ToString(), e.Value.Description, Duration = e.Value.Duration.ToString(), e.Value.Data })
        }));
    }
});
app.MapPrometheusScrapingEndpoint();

ProgramLog.LogServerStarting(app.Logger, ServerInfo.Version);

app.Run();

public partial class Program { }
