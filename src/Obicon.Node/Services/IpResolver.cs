using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Obicon.Node.Services;

/// <summary>
/// Resolves the node's own addresses: the internal (LAN) IPv4 and IPv6 addresses from its
/// network interfaces, and the external (public) addresses by asking check services.
/// The selection and parsing are pure functions so they are unit-testable.
/// </summary>
public static partial class IpResolver
{
    [GeneratedRegex(@"\b((?:\d{1,3}\.){3}\d{1,3})\b")]
    private static partial Regex Ipv4Regex();

    [GeneratedRegex(@"\b[0-9a-fA-F:]*:[0-9a-fA-F:.]+\b")]
    private static partial Regex Ipv6Regex();

    /// <summary>
    /// The node's internal (LAN) IPv4 address: the first non-loopback unicast address of an
    /// up, non-virtual interface, preferring addresses that are not link-local.
    /// Null when no suitable address exists.
    /// </summary>
    public static string? GetInternalIpv4()
    {
        return PickInternalIpv4(EnumerateInternalAddresses(AddressFamily.InterNetwork))?.ToString();
    }

    /// <summary>
    /// The node's internal (LAN) IPv6 address, preferring globally reachable addresses over
    /// link-local ones. Null when no suitable address exists.
    /// </summary>
    public static string? GetInternalIpv6()
    {
        return PickInternalIpv6(EnumerateInternalAddresses(AddressFamily.InterNetworkV6))?.ToString();
    }

    /// <summary>
    /// All unicast addresses of the given family from up, non-virtual interfaces that are
    /// not loopback.
    /// </summary>
    private static IEnumerable<IPAddress> EnumerateInternalAddresses(AddressFamily family)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            var usable = nic.OperationalStatus == OperationalStatus.Up
                && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && !(nic.Description?.Contains("virtual", StringComparison.OrdinalIgnoreCase) ?? false)
                && !(nic.Name?.Contains("virtual", StringComparison.OrdinalIgnoreCase) ?? false);
            if (!usable)
            {
                continue;
            }

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == family && !IPAddress.IsLoopback(unicast.Address))
                {
                    yield return unicast.Address;
                }
            }
        }
    }

    /// <summary>
    /// Picks the best internal IPv4 address from a list: non-link-local (169.254.x.x)
    /// first, then the first found.
    /// </summary>
    public static IPAddress? PickInternalIpv4(IEnumerable<IPAddress> candidates)
    {
        return candidates.Where(a => !IPAddress.IsLoopback(a)).OrderBy(IsLinkLocalIpv4).FirstOrDefault();
    }

    /// <summary>
    /// Picks the best internal IPv6 address from a list: global addresses over
    /// link-local (fe80::), then the first found.
    /// </summary>
    public static IPAddress? PickInternalIpv6(IEnumerable<IPAddress> candidates)
    {
        return candidates
            .Where(a => !IPAddress.IsLoopback(a))
            .OrderBy(a => a.IsIPv6LinkLocal ? 1 : 0)
            .ThenBy(a => a.IsIPv6UniqueLocal ? 1 : 0)
            .FirstOrDefault();
    }

    /// <summary>
    /// Extracts the first IPv4 address from a check service's response body, e.g. "1.2.3.4"
    /// from checkip.amazonaws.com's "1.2.3.4\n". Null when none is found.
    /// </summary>
    public static string? ParseExternalIpv4(string? body)
    {
        var match = Ipv4Regex().Match(body ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        return IPAddress.TryParse(match.Groups[1].Value, out var parsed) ? parsed.ToString() : null;
    }

    /// <summary>
    /// Extracts the first IPv6 address from a check service's response body, e.g.
    /// "2a00:1450:4001:81b::200e" from api6.ipify.org's response. Null when none is found.
    /// </summary>
    public static string? ParseExternalIpv6(string? body)
    {
        var match = Ipv6Regex().Match(body ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        return IPAddress.TryParse(match.Value.TrimEnd('.'), out var parsed) && parsed.AddressFamily == AddressFamily.InterNetworkV6
            ? parsed.ToString()
            : null;
    }

    private static bool IsLinkLocalIpv4(IPAddress address)
    {
        return address.ToString().StartsWith("169.254.", StringComparison.Ordinal);
    }
}
