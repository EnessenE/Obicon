namespace Obicon.Server.Models;

/// <summary>
/// A pool targeted by a test: the test runs on the pool's members. One row per
/// targeted pool; membership cascades on test or pool deletion. Default: empty table.
/// </summary>
public class TestTargetPool
{
    /// <summary>
    /// ID of the test. Part of the primary key, cascades on test deletion.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// ID of the targeted pool. Part of the primary key, cascades on pool deletion.
    /// </summary>
    public Guid PoolId { get; set; }
}
