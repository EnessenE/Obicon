using Obicon.Server.Models;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Metrics;

/// <summary>
/// Emits the per-run test metrics (obicon.tests.runs and obicon.tests.duration_ms) for a
/// finished test job, honoring the TestMetricsEnabled setting and the TestMetricsLabels
/// selection of which labels ride along.
/// </summary>
public interface ITestMetricsEmitter
{
    /// <summary>
    /// Records a finished test run on the metrics, when TestMetricsEnabled is on. The
    /// labels attached come from the TestMetricsLabels setting; test_id and the counter's
    /// status are always present.
    /// </summary>
    Task EmitAsync(TestJob? job, TestResponse? test, NodeResponse? node, TestJobStatus status, double durationMs);
}

/// <summary>
/// Default <see cref="ITestMetricsEmitter"/>: reads both metrics settings through the settings
/// service (cached, so this is cheap on the hot path) and records the run on the Obicon.Tests meter.
/// </summary>
public class TestMetricsEmitter : ITestMetricsEmitter
{
    /// <summary>
    /// Labels the user can select through the TestMetricsLabels setting. Anything else
    /// in the JSON array is ignored.
    /// </summary>
    public static readonly string[] SelectableLabels = ["test_type", "test_name", "node_id", "node_name", "node_labels"];

    /// <summary>
    /// The default selection when TestMetricsLabels is unset or unparseable, matching the
    /// setting's documented default.
    /// </summary>
    public static readonly string[] DefaultLabels = ["test_type", "test_name", "node_name", "node_labels"];

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

        var selected = await GetSelectedLabelsAsync();
        var labels = new List<KeyValuePair<string, object?>>
        {
            // Forced floor: without test_id no series can be attributed
            new("test_id", job?.TestId.ToString() ?? "unknown")
        };

        if (selected.Contains("test_type"))
        {
            labels.Add(new("test_type", job?.TestType.ToString() ?? "unknown"));
        }
        if (selected.Contains("test_name"))
        {
            labels.Add(new("test_name", test?.Name ?? "run-once"));
        }
        if (selected.Contains("node_id"))
        {
            labels.Add(new("node_id", node?.Id.ToString() ?? "unknown"));
        }
        if (selected.Contains("node_name"))
        {
            labels.Add(new("node_name", node?.Name ?? "unknown"));
        }
        if (selected.Contains("node_labels"))
        {
            var nodeLabels = node?.Labels.OrderBy(l => l, StringComparer.Ordinal) ?? Enumerable.Empty<string>();
            labels.Add(new("node_labels", string.Join(",", nodeLabels)));
        }

        ServerMetrics.TestRun(status.ToString(), labels, durationMs);
    }

    /// <summary>
    /// The selected label set from the TestMetricsLabels setting. An explicitly empty
    /// list means only the forced floor rides along; the defaults apply only when the
    /// setting is missing entirely.
    /// </summary>
    public async Task<HashSet<string>> GetSelectedLabelsAsync()
    {
        var selected = await _settingsService.GetAsync<List<string>>("TestMetricsLabels");
        return selected == null
            ? new HashSet<string>(DefaultLabels, StringComparer.Ordinal)
            : new HashSet<string>(selected.Select(l => l.Trim()), StringComparer.Ordinal);
    }
}
