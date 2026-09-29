using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obicon.Node.Configuration;
using Obicon.Node.Metrics;
using Obicon.Node.Services;
using Obicon.Node.Services.TestRunners;
using OpenTelemetry.Metrics;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console());

builder.Services.Configure<NodeSettings>(builder.Configuration.GetSection("Node"));

var metricsPrefix = builder.Configuration["Node:MetricsUrlPrefix"] ?? "http://localhost:9464/";

builder.Services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddMeter(NodeMetrics.NodeMeterName)
        .AddPrometheusHttpListener(o => o.ConfigureHttpListener = (_, listener) => listener.Prefixes.Add(metricsPrefix)));

builder.Services.AddSingleton<ServerConnection>();
builder.Services.AddSingleton<IServerConnection>(sp => sp.GetRequiredService<ServerConnection>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ServerConnection>());

builder.Services.AddSingleton<ITestRunner, PingTestRunner>();
builder.Services.AddSingleton<ITestRunner, TracerouteTestRunner>();
builder.Services.AddSingleton<ITestRunner, HttpTestRunner>();
builder.Services.AddSingleton<ITestRunner, HttpsTestRunner>();
builder.Services.AddSingleton<ITestRunner, TcpTestRunner>();
builder.Services.AddSingleton<ITestRunner, DnsTestRunner>();
builder.Services.AddSingleton<ITestExecutor, TestExecutor>();
builder.Services.AddSingleton<NodeIdentityStore>();
builder.Services.AddSingleton<EnrollmentClient>();

builder.Services.AddHostedService<HealthService>();
builder.Services.AddHostedService<MonitoringService>();

var host = builder.Build();
host.Services.GetRequiredService<ILogger<Program>>().LogInformation("Obicon Node starting");
await host.RunAsync();
