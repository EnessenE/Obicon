namespace Obicon.Server.Models;

/// <summary>
/// A node directly targeted by a test (as opposed to via a pool). One row per
/// targeted node; membership cascades on test or node deletion. Default: empty table.
/// </summary>
public class TestTargetNode
{
    /// <summary>
    /// ID of the test. Part of the primary key, cascades on test deletion.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// ID of the targeted node. Part of the primary key, cascades on node deletion.
    /// </summary>
    public Guid NodeId { get; set; }
}
