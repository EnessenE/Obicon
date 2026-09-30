namespace Obicon.Server.Models.Responses;

/// <summary>
/// Live status of a node: its active flag and whether it is currently connected.
/// </summary>
public class NodeStatusResponse
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
    /// Indicates if the node is enabled on the server.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indicates if the node currently has a live WebSocket connection.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Timestamp of the last heartbeat received from the node. Null if never connected.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// Maps a node API response plus its live connection state to a status response.
    /// </summary>
    /// <param name="node">The node API response.</param>
    /// <param name="isConnected">Whether the node is currently connected.</param>
    public static NodeStatusResponse From(NodeResponse node, bool isConnected) => new()
    {
        Id = node.Id,
        Name = node.Name,
        IsActive = node.IsActive,
        IsConnected = isConnected,
        LastSeenAt = node.LastSeenAt
    };
}
