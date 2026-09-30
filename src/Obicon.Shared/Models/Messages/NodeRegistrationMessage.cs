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

    /// <summary>
    /// Version of the node software, e.g. "0.2.0". The server uses it to reject
    /// incompatible nodes. Default: empty string.
    /// </summary>
    public string NodeVersion { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of tests this node executes at the same time. Null when the node does not report it.
    /// </summary>
    public int? MaxConcurrentTests { get; set; }

    /// <summary>
    /// How often the node sends a heartbeat, in seconds. Null when the node does not report it.
    /// </summary>
    public int? HeartbeatIntervalSeconds { get; set; }

    /// <summary>
    /// Default timeout this node applies to tests without one, in seconds. Null when not reported.
    /// </summary>
    public int? DefaultTestTimeoutSeconds { get; set; }

    /// <summary>
    /// Upper limit this node accepts for test timeouts, in seconds. Null when not reported.
    /// </summary>
    public int? MaxTestTimeoutSeconds { get; set; }

    /// <summary>
    /// Delay this node waits before reconnecting after a disconnect, in seconds. Null when not reported.
    /// </summary>
    public int? ReconnectDelaySeconds { get; set; }

    /// <summary>
    /// The node's internal (LAN) IPv4 address at connection time. Null when unavailable.
    /// </summary>
    public string? InternalIpv4 { get; set; }

    /// <summary>
    /// The node's internal (LAN) IPv6 address at connection time. Null when unavailable.
    /// </summary>
    public string? InternalIpv6 { get; set; }

    /// <summary>
    /// The node's external (public internet) IPv4 address at connection time.
    /// Null when the check has not succeeded yet.
    /// </summary>
    public string? ExternalIpv4 { get; set; }

    /// <summary>
    /// The node's external (public internet) IPv6 address at connection time.
    /// Null when unavailable.
    /// </summary>
    public string? ExternalIpv6 { get; set; }
}
