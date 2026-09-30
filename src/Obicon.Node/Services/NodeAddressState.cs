namespace Obicon.Node.Services;

/// <summary>
/// The node's most recently resolved addresses, shared between the IP monitor that
/// refreshes them and the server connection that reports them at registration.
/// Null fields mean the address family is unavailable on this node.
/// </summary>
public class NodeAddressState
{
    /// <summary>
    /// The node's internal (LAN) IPv4 address. Null until the first check, or when unavailable.
    /// </summary>
    public volatile string? InternalIpv4;

    /// <summary>
    /// The node's internal (LAN) IPv6 address. Null when unavailable.
    /// </summary>
    public volatile string? InternalIpv6;

    /// <summary>
    /// The node's external (public internet) IPv4 address. Null until the first successful check.
    /// </summary>
    public volatile string? ExternalIpv4;

    /// <summary>
    /// The node's external (public internet) IPv6 address. Null when unavailable.
    /// </summary>
    public volatile string? ExternalIpv6;
}
