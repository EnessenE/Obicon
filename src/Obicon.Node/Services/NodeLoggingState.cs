using Serilog.Events;

namespace Obicon.Node.Services;

/// <summary>
/// Shared logging policy state: whether the server allows log shipping, and whether
/// local logging is disabled for test-related output. The local logging policy only
/// mutes test execution logs (executor, runners, monitoring); connection lifecycle,
/// policy changes, and errors always appear on the node's own console.
/// </summary>
public class NodeLoggingState
{
    private const string TestExecutorSource = "Obicon.Node.Services.TestExecutor";
    private const string MonitoringSource = "Obicon.Node.Services.MonitoringService";
    private const string TestRunnersPrefix = "Obicon.Node.Services.TestRunners";

    /// <summary>
    /// Property set on log calls that belong to test activity but are issued by
    /// classes whose other output is lifecycle (e.g. ServerConnection's job assignments).
    /// </summary>
    public const string TestActivityProperty = "TestActivity";

    /// <summary>
    /// Whether the server currently accepts shipped log entries.
    /// Set from the server hello; default false until the first hello arrives.
    /// </summary>
    public volatile bool ServerAllowsLogShipping;

    /// <summary>
    /// The server's default policy for local node logging, from the server hello. Default true.
    /// </summary>
    public volatile bool ServerLocalLoggingEnabled = true;

    /// <summary>
    /// The local logging policy most recently applied (node override resolved against the
    /// server default), used to detect and log changes. Default true.
    /// </summary>
    public volatile bool LastAppliedLocalLogging = true;

    /// <summary>
    /// Whether test-related log entries are currently muted on the node's own console.
    /// Set from the local logging policy; shipping is unaffected.
    /// </summary>
    public volatile bool TestsMuted;

    /// <summary>
    /// Applies the local logging policy: the node's own override wins over the server's
    /// default. A disabled policy mutes only test-related output locally.
    /// </summary>
    public void ApplyLocalLoggingPolicy(bool? nodeOverride, bool serverDefault)
    {
        var effective = nodeOverride ?? serverDefault;
        LastAppliedLocalLogging = effective;
        TestsMuted = !effective;
    }

    /// <summary>
    /// Whether the entry belongs to the node's test execution: anything carrying the
    /// TestActivity marker, plus the executor, the test runners, and the monitoring
    /// service that reports test statistics.
    /// </summary>
    public static bool IsTestRelated(LogEvent logEvent)
    {
        if (logEvent.Properties.ContainsKey(TestActivityProperty))
        {
            return true;
        }

        if (!logEvent.Properties.TryGetValue("SourceContext", out var source))
        {
            return false;
        }

        var context = source.ToString().Trim('"');
        return context == TestExecutorSource
            || context == MonitoringSource
            || context.StartsWith(TestRunnersPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether the entry must be kept off the node's own console right now:
    /// test-related output while the local logging policy has it muted.
    /// </summary>
    public bool ShouldMuteLocally(LogEvent logEvent)
    {
        return TestsMuted && IsTestRelated(logEvent);
    }
}
