namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent by node to register with the server on WebSocket connection.
/// </summary>
public class NodeRegistrationMessage
{
    /// <summary>
    /// Unique identifier of the node.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the node. Default: empty string.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;
}
