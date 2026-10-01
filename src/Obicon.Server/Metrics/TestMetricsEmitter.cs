using Obicon.Server.Models;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Metrics;

/// <summary>
/// Emits the per-run test metrics (obicon.tests.runs and obicon.tests.duration_ms) for a
/// finished test job, honoring the TestMetricsEnabled and TestMetricsIncludeNodeLabels settings.
/// </summary>
public interface ITestMetricsEmitter
{
    /// <summary>
    /// Records a finished test run on the metrics, when TestMetricsEnabled is on. When
    /// TestMetricsIncludeNodeLabels is on, the executing node's labels ride along as the
    /// comma-separated node_labels label.
    /// </summary>
    Task EmitAsync(TestJob? job, TestResponse? test, NodeResponse? node, TestJobStatus status, double durationMs);
}

/// <summary>
/// Default <see cref="ITestMetricsEmitter"/>: reads both metrics settings through the settings
/// service (cached, so this is cheap on the hot path) and records the run on the Obicon.Tests meter.
/// </summary>
public class TestMetricsEmitter : ITestMetricsEmitter
{
    private readonly IServerSettingsService _settingsService;

    public TestMetricsEmitter(IServerSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task EmitAsync(TestJob? job, TestResponse? test, NodeResponse? node, TestJobStatus status, double durationMs)
    {
        if (!await _settingsService.GetAsync<bool>("TestMetricsEnabled"))
        {
            return;
        }

        var nodeLabels = await _settingsService.GetAsync<bool>("TestMetricsIncludeNodeLabels")
            ? string.Join(",", node?.Labels.OrderBy(l => l, StringComparer.Ordinal) ?? Enumerable.Empty<string>())
            : null;

        ServerMetrics.TestRun(
            status.ToString(),
            job?.TestType.ToString() ?? "unknown",
            job?.TestId.ToString() ?? "unknown",
            test?.Name ?? "run-once",
            node?.Id.ToString() ?? "unknown",
            node?.Name ?? "unknown",
            durationMs,
            nodeLabels);
    }
}
