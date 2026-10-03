using System.Globalization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obicon.Node.Configuration;
using Obicon.Node.Metrics;
using Obicon.Node.Services;
using Obicon.Node.Services.TestRunners;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Settings.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<NodeSettings>(builder.Configuration.GetSection("Node"));

// Log capture + shipping: the sink queues every Serilog event, the shipper sends
// them to the server while it allows it, and the state carries the console level
// switch that the server's local-logging policy can silence
builder.Services.AddSingleton<NodeLoggingState>();
builder.Services.AddSingleton<NodeLogSink>();
builder.Services.AddHostedService<NodeLogShipper>();
builder.Services.AddSingleton<NodeAddressState>();
builder.Services.AddHostedService<IpAddressMonitor>();

builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    var loggingState = services.GetRequiredService<NodeLoggingState>();
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        // Debug as the pipeline minimum so the capture sink sees everything;
        // the console sink's own level comes from its appsettings section
        .MinimumLevel.Debug()
        // The console sink is defined in appsettings (Serilog:ConsoleSink) but wrapped
        // in a conditional so the local logging policy can drop test-related output
        // per event; connection lifecycle and policy changes always appear. When the
        // section defines no sinks, the built-in default below takes over
        .WriteTo.Conditional(
            e => !loggingState.ShouldMuteLocally(e),
            sinkConfig =>
            {
                if (builder.Configuration.GetSection("Serilog:ConsoleSink:WriteTo").GetChildren().Any())
                {
                    sinkConfig.Logger(consoleConfig => consoleConfig
                        .MinimumLevel.Verbose()
                        .ReadFrom.Configuration(
                            builder.Configuration,
                            new ConfigurationReaderOptions { SectionName = "Serilog:ConsoleSink" }));
                    return;
                }

                // Mirrors the Serilog:ConsoleSink section in appsettings.json
                sinkConfig.Console(
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture);
            })
        .WriteTo.Sink(services.GetRequiredService<NodeLogSink>());
});

// The metrics listener address: Node:MetricsHost and Node:MetricsPort (defaults localhost:9464)
var metricsHost = builder.Configuration["Node:MetricsHost"] ?? "localhost";
if (!int.TryParse(builder.Configuration["Node:MetricsPort"], out var metricsPort) || metricsPort <= 0)
{
    metricsPort = 9464;
}

builder.Services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddMeter(NodeMetrics.NodeMeterName)
        .AddPrometheusHttpListener(o => o.ConfigureHttpListener = (_, listener) =>
        {
            // The exporter keeps its default prefix (http://localhost:9464/) even when a
            // callback is set, so clear it first: a custom port must free the default,
            // and hosts like "+" (all interfaces) are only valid at the listener level
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://{metricsHost}:{metricsPort}/");
        }));

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
var logger = host.Services.GetRequiredService<ILogger<Program>>();
ProgramLog.LogStarting(logger, NodeInfo.Version, metricsHost, metricsPort);
await host.RunAsync();
