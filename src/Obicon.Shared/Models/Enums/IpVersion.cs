namespace Obicon.Shared.Models.Enums;

/// <summary>
/// IP protocol version a test should use.
/// </summary>
public enum IpVersion
{
    /// <summary>
    /// Use whatever the host resolves to, without preferring a family.
    /// </summary>
    Any,

    /// <summary>
    /// Force IPv4; fails if the target has no A record.
    /// </summary>
    Ipv4,

    /// <summary>
    /// Force IPv6; fails if the target has no AAAA record.
    /// </summary>
    Ipv6
}
