namespace Obicon.Server.Models.Responses;

/// <summary>
/// Result of a successful node enrollment. The auth token is returned once so the node
/// can connect; it is also retrievable via the normal node endpoints.
/// </summary>
public class EnrollResponse
{
    /// <summary>
    /// Unique identifier of the node.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the node.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// WebSocket authentication token for the node.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Labels attached to the node.
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// IDs of the pools the node was put into. Default: empty list.
    /// </summary>
    public List<Guid> PoolIds { get; set; } = new();
}
