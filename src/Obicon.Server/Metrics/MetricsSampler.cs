using Obicon.Server.Services;

namespace Obicon.Server.Metrics;

/// <summary>
/// Samples server state on an interval and publishes it to the observable gauges:
/// the test job counts by status (<c>obicon.tests.queue_jobs</c>) and the latest
/// result of every created test (<c>obicon.tests.current_result</c>), so both can
/// be tracked in Prometheus over time. Also runs the finished-job retention sweep
/// once a minute, deleting terminal jobs older than the JobRetentionDays setting.
/// </summary>
public partial class MetricsSampler : BackgroundService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetentionSweepInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Safety cap for jobs stuck in a live status (Queued, Assigned, Running): no test
    /// runs this long, so anything older can only be a zombie row and is swept away.
    /// </summary>
    private static readonly TimeSpan StuckJobCap = TimeSpan.FromDays(7);

    private readonly ITestQueueService _queueService;
    private readonly IServerSettingsService _settingsService;
    private readonly ILogger<MetricsSampler> _logger;

    private DateTime _lastRetentionSweep = DateTime.MinValue;

    public MetricsSampler(ITestQueueService queueService, IServerSettingsService settingsService, ILogger<MetricsSampler> logger)
    {
        _queueService = queueService;
        _settingsService = settingsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var counts = await _queueService.GetStatusCountsAsync();
                ServerMetrics.UpdateQueueCounts(counts.ToDictionary(
                    kv => kv.Key.ToString(), kv => (long)kv.Value));
            }
            catch (Exception ex)
            {
                LogQueueSamplingFailed(ex);
            }

            try
            {
                var results = await _queueService.GetCurrentResultsAsync();
                ServerMetrics.UpdateCurrentResults(results
                    .Select(r => (r.TestId.ToString(), r.TestName, StatusName(r.LatestStatus), r.LatestStatus is null ? -1L : (long)r.LatestStatus))
                    .ToList());
            }
            catch (Exception ex)
            {
                LogResultSamplingFailed(ex);
            }

            // The retention sweep piggybacks on the sampler cadence rather than running
            // as its own hosted service; only terminal rows are ever deleted
            if (DateTime.UtcNow - _lastRetentionSweep >= RetentionSweepInterval)
            {
                _lastRetentionSweep = DateTime.UtcNow;
                await SweepRetainedJobsAsync();
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepRetainedJobsAsync()
    {
        try
        {
            var retentionDays = Math.Max(1, await _settingsService.GetAsync<int>("JobRetentionDays"));
            var finishedBefore = DateTime.UtcNow - TimeSpan.FromDays(retentionDays);
            var stuckBefore = DateTime.UtcNow - StuckJobCap;

            var pruned = await _queueService.PruneJobsAsync(finishedBefore, stuckBefore);
            if (pruned > 0)
            {
                LogRetentionSweepPruned(pruned, retentionDays);
                Metrics.ServerMetrics.Action("jobs_pruned");
            }
        }
        catch (Exception ex)
        {
            LogRetentionSweepFailed(ex);
        }
    }

    private static string StatusName(Shared.Models.Enums.TestJobStatus? status)
    {
        return status?.ToString() ?? "NeverRun";
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Queue metrics sampling failed; retrying on the next interval")]
    private partial void LogQueueSamplingFailed(System.Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Test result sampling failed; retrying on the next interval")]
    private partial void LogResultSamplingFailed(System.Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention sweep deleted {Count} jobs older than {RetentionDays} days")]
    private partial void LogRetentionSweepPruned(int count, int retentionDays);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Job retention sweep failed; retrying on the next interval")]
    private partial void LogRetentionSweepFailed(System.Exception exception);
}
