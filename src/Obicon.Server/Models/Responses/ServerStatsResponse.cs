namespace Obicon.Server.Models.Responses;

/// <summary>
/// Aggregated statistics about the server, exposed at GET /v1/server/stats.
/// </summary>
public class ServerStatsResponse
{
    /// <summary>
    /// Number of tests currently configured.
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Number of tests that are active and should be scheduled. Default: 0.
    /// </summary>
    public int ActiveTests { get; set; }

    /// <summary>
    /// Number of registered nodes.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Number of nodes currently connected via WebSocket.
    /// </summary>
    public int ConnectedNodes { get; set; }

    /// <summary>
    /// Number of jobs waiting in the queue.
    /// </summary>
    public int QueuedJobs { get; set; }

    /// <summary>
    /// Number of jobs currently assigned or running. Default: 0.
    /// </summary>
    public int RunningJobs { get; set; }

    /// <summary>
    /// Number of jobs completed successfully.
    /// </summary>
    public int CompletedJobs { get; set; }

    /// <summary>
    /// Number of jobs that failed.
    /// </summary>
    public int FailedJobs { get; set; }

    /// <summary>
    /// Number of jobs that timed out.
    /// </summary>
    public int TimedOutJobs { get; set; }

    /// <summary>
    /// Number of jobs marked NoRun because their node never acknowledged or started them.
    /// </summary>
    public int NoRunJobs { get; set; }

    /// <summary>
    /// Server uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Current UTC time of the response.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
