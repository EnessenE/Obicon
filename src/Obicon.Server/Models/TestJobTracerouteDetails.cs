namespace Obicon.Server.Models;

/// <summary>
/// Traceroute section of a run's details: one row per finished traceroute job.
/// </summary>
public class TestJobTracerouteDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Address the target resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Whether the trace reached the target. Default: false.
    /// </summary>
    public bool TargetReached { get; set; }

    /// <summary>
    /// Number of hops recorded. Default: 0.
    /// </summary>
    public int HopCount { get; set; }

    /// <summary>
    /// One row per hop, ordered by the Hop column. Default: empty list.
    /// </summary>
    public List<TestJobTracerouteHop> Hops { get; set; } = [];
}
