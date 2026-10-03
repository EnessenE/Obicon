namespace Obicon.Node.Services;

/// <summary>
/// The node's most recently resolved addresses, shared between the IP monitor that
/// refreshes them and the server connection that reports them at registration.
/// Null values mean the address family is unavailable on this node.
/// </summary>
public class NodeAddressState
{
    private volatile string? _internalIpv4;
    private volatile string? _internalIpv6;
    private volatile string? _externalIpv4;
    private volatile string? _externalIpv6;

    /// <summary>
    /// The node's internal (LAN) IPv4 address. Null until the first check, or when unavailable.
    /// </summary>
    public string? InternalIpv4
    {
        get => _internalIpv4;
        set => _internalIpv4 = value;
    }

    /// <summary>
    /// The node's internal (LAN) IPv6 address. Null when unavailable.
    /// </summary>
    public string? InternalIpv6
    {
        get => _internalIpv6;
        set => _internalIpv6 = value;
    }

    /// <summary>
    /// The node's external (public internet) IPv4 address. Null until the first successful check.
    /// </summary>
    public string? ExternalIpv4
    {
        get => _externalIpv4;
        set => _externalIpv4 = value;
    }

    /// <summary>
    /// The node's external (public internet) IPv6 address. Null when unavailable.
    /// </summary>
    public string? ExternalIpv6
    {
        get => _externalIpv6;
        set => _externalIpv6 = value;
    }
}
