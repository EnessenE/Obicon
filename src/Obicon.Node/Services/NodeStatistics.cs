namespace Obicon.Node.Services;

/// <summary>
/// Counters describing the test execution state of the node. Running and Pending are gauges; the rest are monotonic.
/// </summary>
public class NodeStatistics
{
    /// <summary>
    /// Number of tests currently executing. Default: 0.
    /// </summary>
    public long Running;

    /// <summary>
    /// Number of assigned tests waiting for a free execution slot. Default: 0.
    /// </summary>
    public long Pending;

    /// <summary>
    /// Number of tests finished successfully. Default: 0.
    /// </summary>
    public long Completed;

    /// <summary>
    /// Number of tests that finished unsuccessfully. Default: 0.
    /// </summary>
    public long Failed;

    /// <summary>
    /// Number of tests that exceeded their timeout. Default: 0.
    /// </summary>
    public long TimedOut;
}
