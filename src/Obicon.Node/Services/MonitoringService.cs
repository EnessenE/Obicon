using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;

namespace Obicon.Node.Services;

/// <summary>
/// Dedicated monitoring task: periodically logs node state and reports it to the health payload consumers.
/// </summary>
public partial class MonitoringService : BackgroundService
{
    private readonly NodeSettings _settings;
    private readonly IServerConnection _serverConnection;
    private readonly ITestExecutor _testExecutor;
    private readonly ILogger<MonitoringService> _logger;

    public MonitoringService(
        IOptions<NodeSettings> settings,
        IServerConnection serverConnection,
        ITestExecutor testExecutor,
        ILogger<MonitoringService> logger)
    {
        _settings = settings.Value;
        _serverConnection = serverConnection;
        _testExecutor = testExecutor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _settings.MonitoringIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var stats = _testExecutor.Statistics;
            LogMonitor(_serverConnection.IsConnected, stats.Running, stats.Pending, stats.Completed, stats.Failed, stats.TimedOut);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Monitor: serverConnected={Connected} running={Running} pending={Pending} completed={Completed} failed={Failed} timedOut={TimedOut}")]
    private partial void LogMonitor(bool connected, long running, long pending, long completed, long failed, long timedOut);
}
