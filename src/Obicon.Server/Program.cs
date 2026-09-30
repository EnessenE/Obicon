using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Obicon.Server.BackgroundServices;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.Health;
using Obicon.Server.Metrics;
using Obicon.Server.Middleware;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .WriteTo.Console()
    .ReadFrom.Configuration(context.Configuration));

builder.Services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(ServerMetrics.ServerMeterName)
        .AddMeter(ServerMetrics.TestsMeterName)
        .AddPrometheusExporter());

builder.Services.AddCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Obicon API", Version = "v1" });
    c.AddSecurityDefinition("Authorization", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Enter 'uwu' for authorization"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Authorization" } }, new string[] {} }
    });
});
builder.Services.AddSingleton<INodeService, NodeService>();
builder.Services.AddSingleton<INodePoolService, NodePoolService>();
builder.Services.AddSingleton<ITestService, TestService>();
builder.Services.AddSingleton<ITestQueueService, TestQueueService>();
builder.Services.AddSingleton<NodeConnectionManager>();
builder.Services.AddSingleton<Obicon.Server.Data.SqliteWriteQueue>();
builder.Services.AddSingleton<IConfigRepository, JsonConfigRepository>();
builder.Services.AddDbContextFactory<ObiconDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// The ServerSettings section is the configuration layer of the settings system:
// values present there (or as ServerSettings__* environment variables) are forced and read-only
builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));
builder.Services.AddSingleton<IServerSettingsService, ServerSettingsService>();
builder.Services.AddSingleton<NodePolicyBroadcaster>();
builder.Services.AddSingleton<IEnrollTokenService, EnrollTokenService>();
builder.Services.AddSingleton<INodeEnrollmentService, NodeEnrollmentService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddHostedService<TestQueueProcessor>();
builder.Services.AddHostedService<TestScheduler>();
builder.Services.AddHostedService<Obicon.Server.Metrics.MetricsSampler>();

var app = builder.Build();

// Create the SQLite schema on startup if the database does not exist yet
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ObiconDbContext>();
    await db.Database.EnsureCreatedAsync();

    // EnsureCreated only builds an empty database; reconcile older schemas in place
    SchemaMigrator.Migrate(db);
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

app.Logger.LogInformation("Obicon Server v{Version} starting", ServerInfo.Version);

app.Run();

public partial class Program { }
