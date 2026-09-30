using System.Net.WebSockets;

namespace Obicon.Server.WebSockets;

/// <summary>
/// Represents an active WebSocket connection from a node to the server.
/// </summary>
public class NodeConnection
{
    /// <summary>
    /// Unique identifier of the connected node.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the connected node.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// The WebSocket connection instance.
    /// </summary>
    public WebSocket Socket { get; set; } = null!;

    /// <summary>
    /// Timestamp when the connection was established.
    /// </summary>
    public DateTime ConnectedAt { get; set; }

    /// <summary>
    /// Timestamp of the last heartbeat or message received.
    /// </summary>
    public DateTime LastSeenAt { get; set; }
}
