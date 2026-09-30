namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent periodically by node to indicate it's alive.
/// </summary>
public class NodeHeartbeatMessage
{
    /// <summary>
    /// Unique identifier of the node sending the heartbeat.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the heartbeat was sent.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
