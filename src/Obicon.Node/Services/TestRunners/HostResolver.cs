using System.Net;
using System.Net.Sockets;
using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Resolves hostnames honoring the requested IP version.
/// </summary>
public static class HostResolver
{
    /// <summary>
    /// Resolves a host to a single address of the requested family. Returns null if the host
    /// has no address of that family; throws SocketException if resolution fails entirely.
    /// </summary>
    /// <param name="host">Hostname or IP literal.</param>
    /// <param name="ipVersion">Requested IP version.</param>
    /// <param name="cancellationToken">Cancelled when the test times out.</param>
    public static async Task<IPAddress?> ResolveAsync(string host, IpVersion ipVersion, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return Matches(literal, ipVersion) ? literal : null;
        }

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        return addresses.FirstOrDefault(a => Matches(a, ipVersion));
    }

    /// <summary>
    /// Resolves all addresses of a host filtered to the requested family.
    /// </summary>
    /// <param name="host">Hostname or IP literal.</param>
    /// <param name="ipVersion">Requested IP version.</param>
    public static IPAddress[] ResolveAll(string host, IpVersion ipVersion)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return Matches(literal, ipVersion) ? [literal] : [];
        }

        var addresses = Dns.GetHostAddresses(host);
        return ipVersion == IpVersion.Any ? addresses : addresses.Where(a => Matches(a, ipVersion)).ToArray();
    }

    /// <summary>
    /// Checks whether an address belongs to the requested family.
    /// </summary>
    public static bool Matches(IPAddress address, IpVersion ipVersion) => ipVersion switch
    {
        IpVersion.Ipv4 => address.AddressFamily == AddressFamily.InterNetwork,
        IpVersion.Ipv6 => address.AddressFamily == AddressFamily.InterNetworkV6,
        _ => true
    };

    /// <summary>
    /// Human-readable family name used in failure messages.
    /// </summary>
    public static string FamilyName(IpVersion ipVersion) => ipVersion switch
    {
        IpVersion.Ipv4 => "IPv4",
        IpVersion.Ipv6 => "IPv6",
        _ => "any"
    };
}
