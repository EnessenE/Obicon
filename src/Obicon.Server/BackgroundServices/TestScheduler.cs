using Obicon.Server.Configuration;
using Obicon.Server.Services;

namespace Obicon.Server.BackgroundServices;

/// <summary>
/// Runs active tests on their configured frequency. The loop interval equals the lowest
/// configured FrequencyPresetsSeconds preset, so the fastest test fires on time; the
/// interval is re-read every cycle, so changing the setting takes effect without a restart.
/// Overdue tests (e.g. after server downtime) run once and resynchronize.
/// </summary>
public partial class TestScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TestScheduler> _logger;

    private int _lastInterval;

    public TestScheduler(IServiceScopeFactory scopeFactory, ILogger<TestScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = _lastInterval > 0 ? _lastInterval : 5;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();
                var presets = FrequencyPresets.Normalize(await settingsService.GetAsync<List<int>>("FrequencyPresetsSeconds"));
                interval = Math.Max(1, presets.Min());

                if (interval != _lastInterval)
                {
                    LogScanInterval(interval);
                    _lastInterval = interval;
                }

                var testService = scope.ServiceProvider.GetRequiredService<ITestService>();
                await testService.ScheduleDueTestsAsync();
            }
            catch (Exception ex)
            {
                LogLoopError(ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        LogStopped();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "TestScheduler started")]
    private partial void LogStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "TestScheduler stopped")]
    private partial void LogStopped();

    [LoggerMessage(Level = LogLevel.Information, Message = "TestScheduler scans every {Interval}s (lowest frequency preset)")]
    private partial void LogScanInterval(int interval);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in TestScheduler")]
    private partial void LogLoopError(System.Exception exception);
}
