namespace Obicon.Server.Models;

/// <summary>
/// A user-defined pool that groups nodes, e.g. by location or purpose.
/// A node can belong to multiple pools.
/// </summary>
public class NodePool
{
    /// <summary>
    /// Unique identifier for the pool. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the pool. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Free-form description of the pool, shown in the UI. Default: empty string.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The pool's member nodes; one row per member in the pool_members table. Default: empty list.
    /// </summary>
    public List<PoolMember> Members { get; set; } = new();

    /// <summary>
    /// Timestamp when the pool was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
