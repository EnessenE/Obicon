using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obicon.Server.Services;

namespace Obicon.Server.Metrics;

/// <summary>
/// Samples server state on an interval and publishes it to the observable gauges:
/// the test job counts by status (<c>obicon.tests.queue_jobs</c>) and the latest
/// result of every created test (<c>obicon.tests.current_result</c>), so both can
/// be tracked in Prometheus over time.
/// </summary>
public class MetricsSampler : BackgroundService
{
    private readonly ITestQueueService _queueService;
    private readonly ILogger<MetricsSampler> _logger;

    public MetricsSampler(ITestQueueService queueService, ILogger<MetricsSampler> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
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
                _logger.LogDebug(ex, "Queue metrics sampling failed; retrying on the next interval");
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
                _logger.LogDebug(ex, "Test result sampling failed; retrying on the next interval");
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

    private static string StatusName(Shared.Models.Enums.TestJobStatus? status)
    {
        return status?.ToString() ?? "NeverRun";
    }
}
