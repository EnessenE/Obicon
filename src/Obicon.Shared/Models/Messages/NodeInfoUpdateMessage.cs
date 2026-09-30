namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Sent by node when it has refreshed its own address information, so the server can
/// keep it current without a reconnect. The server identifies the node by its connection;
/// the NodeId field is informational.
/// </summary>
public class NodeInfoUpdateMessage
{
    /// <summary>
    /// Unique identifier of the node that refreshed its addresses. Default: empty string.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// The node's internal (LAN) IPv4 address, e.g. "192.168.1.42". Null when unavailable.
    /// </summary>
    public string? InternalIpv4 { get; set; }

    /// <summary>
    /// The node's internal (LAN) IPv6 address, e.g. "fd00::42". Null when unavailable.
    /// </summary>
    public string? InternalIpv6 { get; set; }

    /// <summary>
    /// The node's external (public internet) IPv4 address as reported by the check service.
    /// Null when the check has not succeeded yet.
    /// </summary>
    public string? ExternalIpv4 { get; set; }

    /// <summary>
    /// The node's external (public internet) IPv6 address as reported by the IPv6 check
    /// service. Null when unavailable (e.g. no IPv6 connectivity).
    /// </summary>
    public string? ExternalIpv6 { get; set; }
}
