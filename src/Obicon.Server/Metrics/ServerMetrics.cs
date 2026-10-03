using System.Diagnostics.Metrics;
using Obicon.Server.Configuration;

namespace Obicon.Server.Metrics;

/// <summary>
/// Server-side OpenTelemetry meters: Obicon.Tests for test executions and
/// Obicon.Server for server lifecycle actions. Exported on /metrics.
/// </summary>
public class ServerMetrics
{
    public const string ServerMeterName = "Obicon.Server";
    public const string TestsMeterName = "Obicon.Tests";

    private static readonly Meter ServerMeter = new(ServerMeterName);
    private static readonly Meter TestsMeter = new(TestsMeterName);

    private static readonly Counter<long> TestRuns = TestsMeter.CreateCounter<long>(
        "obicon.tests.runs", description: "Completed test runs");

    private static readonly Histogram<double> TestDuration = TestsMeter.CreateHistogram<double>(
        "obicon.tests.duration_ms", unit: "ms", description: "Duration of test executions");

    private static readonly Counter<long> ServerActions = ServerMeter.CreateCounter<long>(
        "obicon.server.actions", description: "Server lifecycle actions");

    private static readonly Counter<long> NoRuns = ServerMeter.CreateCounter<long>(
        "obicon.server.noruns", description: "Jobs marked NoRun, by reason");

    private static readonly Counter<long> NodeLogs = ServerMeter.CreateCounter<long>(
        "obicon.server.nodelogs", description: "Log entries received from nodes, by level and source");

    private static readonly ObservableGauge<long> QueueJobs = TestsMeter.CreateObservableGauge<long>(
        "obicon.tests.queue_jobs",
        () => ObserveQueueJobs(),
        description: "Current test jobs by status, so the queue state can be tracked in Prometheus over time");

    private static readonly ObservableGauge<long> BuildInfo = ServerMeter.CreateObservableGauge<long>(
        "obicon.server.build_info",
        () => new Measurement<long>(1, new KeyValuePair<string, object?>("version", ServerInfo.Version)),
        description: "Server build information; the version label carries the server version and the value is always 1");

    private static readonly ObservableGauge<long> CurrentResults = TestsMeter.CreateObservableGauge<long>(
        "obicon.tests.current_result",
        () => ObserveCurrentResults(),
        description: "Latest job status per created test: 0=Queued 1=Assigned 2=Running 3=Completed 4=Failed 5=Timeout 6=NoRun, -1=never ran");

    /// <summary>
    /// Latest test results per test id, swapped in atomically by the metrics sampler.
    /// The value is the latest job's <see cref="Obicon.Shared.Models.Enums.TestJobStatus"/>, or -1 when the test never ran.
    /// </summary>
    private static volatile IReadOnlyList<(string TestId, string TestName, string Status, long Value)> _currentResults = Array.Empty<(string, string, string, long)>();

    /// <summary>
    /// Publishes a fresh per-test result snapshot to the <c>obicon.tests.current_result</c> gauge.
    /// </summary>
    public static void UpdateCurrentResults(IReadOnlyList<(string TestId, string TestName, string Status, long Value)> results)
    {
        _currentResults = results;
    }

    private static IEnumerable<Measurement<long>> ObserveCurrentResults()
    {
        return _currentResults.Select(r => new Measurement<long>(
            r.Value,
            new KeyValuePair<string, object?>("test_id", r.TestId),
            new KeyValuePair<string, object?>("test_name", r.TestName),
            new KeyValuePair<string, object?>("status", r.Status)));
    }

    /// <summary>
    /// Latest queue snapshot per status name, swapped in atomically by <see cref="MetricsSampler"/>.
    /// </summary>
    private static volatile IReadOnlyDictionary<string, long> _queueCounts = new Dictionary<string, long>();

    /// <summary>
    /// Publishes a fresh queue snapshot to the <c>obicon.tests.queue_jobs</c> gauge.
    /// </summary>
    public static void UpdateQueueCounts(IReadOnlyDictionary<string, long> counts)
    {
        _queueCounts = counts;
    }

    private static IEnumerable<Measurement<long>> ObserveQueueJobs()
    {
        return _queueCounts.Select(kv =>
            new Measurement<long>(kv.Value, new KeyValuePair<string, object?>("status", kv.Key)));
    }

    /// <summary>
    /// Counts a log entry shipped by a node, labeled with its level, source context,
    /// and the node's identity.
    /// </summary>
    public static void NodeLog(string level, string sourceContext, string nodeId, string nodeName)
    {
        NodeLogs.Add(1, new KeyValuePair<string, object?>[]
        {
            new("level", level),
            new("source_context", sourceContext),
            new("node_id", nodeId),
            new("node_name", nodeName)
        });
    }

    /// <summary>
    /// Records a finished test run, labeled per the caller's selection (built by
    /// <see cref="TestMetricsEmitter"/> from the TestMetricsLabels setting). The
    /// counter adds the forced "status" label on top of the given labels.
    /// </summary>
    public static void TestRun(string status, IReadOnlyList<KeyValuePair<string, object?>> labels, double durationMs)
    {
        var counterLabels = new List<KeyValuePair<string, object?>>(labels)
        {
            new("status", status)
        };

        TestRuns.Add(1, counterLabels.ToArray());
        TestDuration.Record(durationMs, labels.ToArray());
    }

    /// <summary>
    /// Records a basic server action, e.g. created_node, deleted_pool, token_regenerated.
    /// </summary>
    public static void Action(string action)
    {
        ServerActions.Add(1, new KeyValuePair<string, object?>("action", action));
    }

    /// <summary>
    /// Records a job marked NoRun, with the reason (never_acknowledged or never_started).
    /// </summary>
    public static void NoRun(string reason)
    {
        NoRuns.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }
}
