namespace Obicon.Server.Models;

/// <summary>
/// Pool membership: one row per node that belongs to a pool. Default: empty table.
/// </summary>
public class PoolMember
{
    /// <summary>
    /// ID of the pool. Part of the primary key, cascades on pool deletion.
    /// </summary>
    public Guid PoolId { get; set; }

    /// <summary>
    /// ID of the member node. Part of the primary key, cascades on node deletion.
    /// </summary>
    public Guid NodeId { get; set; }
}
