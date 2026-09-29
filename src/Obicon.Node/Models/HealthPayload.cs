namespace Obicon.Node.Models;

/// <summary>
/// Payload returned by the node /health endpoint.
/// </summary>
public class HealthPayload
{
    /// <summary>
    /// Health status of the node. Default: "Healthy".
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Indicates if the node is currently connected to the primary server.
    /// </summary>
    public bool ConnectedToServer { get; set; }

    /// <summary>
    /// Number of tests currently executing. Default: 0.
    /// </summary>
    public long RunningTests { get; set; }

    /// <summary>
    /// Number of tests waiting for a free execution slot. Default: 0.
    /// </summary>
    public long PendingTests { get; set; }

    /// <summary>
    /// Number of tests finished successfully since the node started. Default: 0.
    /// </summary>
    public long CompletedTests { get; set; }

    /// <summary>
    /// Number of tests failed since the node started. Default: 0.
    /// </summary>
    public long FailedTests { get; set; }

    /// <summary>
    /// Number of tests that timed out since the node started. Default: 0.
    /// </summary>
    public long TimedOutTests { get; set; }

    /// <summary>
    /// Uptime of the node process.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Current UTC time of the response.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
