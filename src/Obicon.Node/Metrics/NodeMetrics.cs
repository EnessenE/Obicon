using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Obicon.Node.Metrics;

/// <summary>
/// Node-side OpenTelemetry meter (Obicon.Node) for node performance,
/// exported via the Prometheus HttpListener.
/// </summary>
public class NodeMetrics
{
    public const string NodeMeterName = "Obicon.Node";

    private static readonly Meter NodeMeter = new(NodeMeterName);

    private static readonly Counter<long> TestsExecuted = NodeMeter.CreateCounter<long>(
        "obicon.node.tests_executed", description: "Test executions on this node");

    private static readonly Histogram<double> TestDuration = NodeMeter.CreateHistogram<double>(
        "obicon.node.test_duration_ms", unit: "ms", description: "Duration of test executions on this node");

    private static readonly Counter<long> Heartbeats = NodeMeter.CreateCounter<long>(
        "obicon.node.heartbeats", description: "Heartbeats sent to the primary server");

    private static readonly Counter<long> Reconnects = NodeMeter.CreateCounter<long>(
        "obicon.node.reconnects", description: "Reconnect attempts to the primary server");

    /// <summary>
    /// Records a finished test execution with its final status and duration.
    /// </summary>
    public static void TestExecuted(string status, string testType, double durationMs)
    {
        TestsExecuted.Add(1, new KeyValuePair<string, object?>("status", status), new KeyValuePair<string, object?>("test_type", testType));
        TestDuration.Record(durationMs, new KeyValuePair<string, object?>("test_type", testType));
    }

    /// <summary>
    /// Records a heartbeat sent to the primary server.
    /// </summary>
    public static void Heartbeat()
    {
        Heartbeats.Add(1);
    }

    /// <summary>
    /// Records a reconnect attempt.
    /// </summary>
    public static void Reconnect()
    {
        Reconnects.Add(1);
    }
}
