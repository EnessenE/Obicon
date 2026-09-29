using System.Diagnostics;
using System.Diagnostics.Metrics;

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

    /// <summary>
    /// Records a finished test run, labeled per test and per node.
    /// </summary>
    public static void TestRun(
        string status,
        string testType,
        string testId,
        string testName,
        string nodeId,
        string nodeName,
        double durationMs)
    {
        var labels = new KeyValuePair<string, object?>[]
        {
            new("status", status),
            new("test_type", testType),
            new("test_id", testId),
            new("test_name", testName),
            new("node_id", nodeId),
            new("node_name", nodeName)
        };

        TestRuns.Add(1, labels);
        TestDuration.Record(durationMs, labels.Where(l => l.Key is "test_type" or "test_id" or "test_name" or "node_id" or "node_name").ToArray());
    }

    /// <summary>
    /// Records a basic server action, e.g. created_node, deleted_pool, token_regenerated.
    /// </summary>
    public static void Action(string action)
    {
        ServerActions.Add(1, new KeyValuePair<string, object?>("action", action));
    }
}
