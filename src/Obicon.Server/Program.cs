using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Obicon.Server.BackgroundServices;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
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
        .AddHttpClientInstrumentation());

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
builder.Services.AddSingleton<ITestService, TestService>();
builder.Services.AddSingleton<ITestQueueService, TestQueueService>();
builder.Services.AddSingleton<NodeConnectionManager>();
builder.Services.AddSingleton<IConfigRepository, JsonConfigRepository>();
builder.Services.AddDbContextFactory<ObiconDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHostedService<TestQueueProcessor>();

var app = builder.Build();

// Create the SQLite schema on startup if the database does not exist yet
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ObiconDbContext>();
    await db.Database.EnsureCreatedAsync();
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

app.Run();
