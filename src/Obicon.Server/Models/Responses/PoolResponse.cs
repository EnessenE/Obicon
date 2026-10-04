namespace Obicon.Server.Models.Responses;

/// <summary>
/// A node pool as exposed by the API.
/// </summary>
public class PoolResponse
{
    /// <summary>
    /// Unique identifier of the pool.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the pool.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Free-form description of the pool. Default: empty string.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// IDs of the nodes in this pool. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// Timestamp when the pool was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Maps a NodePool entity to its API response.
    /// </summary>
    /// <param name="pool">The pool entity to map.</param>
    public static PoolResponse From(Models.NodePool pool) => new()
    {
        Id = pool.Id,
        Name = pool.Name,
        Description = pool.Description,
        NodeIds = pool.Members.Select(m => m.NodeId).ToList(),
        CreatedAt = pool.CreatedAt
    };
}
