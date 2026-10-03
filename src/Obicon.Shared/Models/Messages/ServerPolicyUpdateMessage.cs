namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Sent by server to connected nodes when a node-facing setting changes at runtime,
/// so nodes apply the new policy without reconnecting. A node's own configuration
/// still takes precedence over the local logging default.
/// </summary>
public class ServerPolicyUpdateMessage
{
    /// <summary>
    /// The server's NodeLogShippingEnabled setting: whether nodes may ship their log
    /// entries to the server. Default: false.
    /// </summary>
    public bool LogShippingEnabled { get; set; }

    /// <summary>
    /// The server's NodeLocalLoggingEnabled setting: the default policy for whether nodes
    /// log locally. A node's own configuration takes precedence over this default.
    /// Default: true.
    /// </summary>
    public bool NodeLocalLoggingEnabled { get; set; } = true;

    /// <summary>
    /// The server's NodeExternalIpResolvingEnabled setting: whether nodes may resolve
    /// their external (public) addresses via the configured check services. Nodes keep
    /// the addresses unavailable while false. Default: false.
    /// </summary>
    public bool ExternalIpResolvingEnabled { get; set; }
}
