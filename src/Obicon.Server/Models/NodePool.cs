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
    /// IDs of the nodes in this pool. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// Timestamp when the pool was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
