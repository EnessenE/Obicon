using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obicon.Node.Configuration;
using Obicon.Node.Services;
using Obicon.Node.Services.TestRunners;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console());

builder.Services.Configure<NodeSettings>(builder.Configuration.GetSection("Node"));

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

builder.Services.AddHostedService<HealthService>();
builder.Services.AddHostedService<MonitoringService>();

var host = builder.Build();
host.Services.GetRequiredService<ILogger<Program>>().LogInformation("Obicon Node starting");
await host.RunAsync();
