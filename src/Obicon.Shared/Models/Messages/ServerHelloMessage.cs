namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent by the server right after accepting a node's WebSocket connection.
/// Lets the node log the server version and check whether it supports it.
/// </summary>
public class ServerHelloMessage
{
    /// <summary>
    /// Version of the server, e.g. "0.2.0". Default: empty string.
    /// </summary>
    public string ServerVersion { get; set; } = string.Empty;

    /// <summary>
    /// The server's NodeLogShippingEnabled setting: whether nodes may ship their log
    /// entries to the server. Nodes stop shipping when false. Default: false.
    /// </summary>
    public bool LogShippingEnabled { get; set; }

    /// <summary>
    /// The server's NodeLocalLoggingEnabled setting: the default policy for whether nodes
    /// write logs locally. A node's own configuration takes precedence over this default.
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
