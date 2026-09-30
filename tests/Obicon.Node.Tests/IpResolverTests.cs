using System.Net;
using System.Net.Sockets;
using Obicon.Node.Services;
using Xunit;

namespace Obicon.Node.Tests;

/// <summary>
/// Unit tests for the node's address resolution: internal address selection from
/// network interfaces, and parsing the external addresses from check services.
/// </summary>
public class IpResolverTests
{
    [Fact]
    public void ParseExternalIpv4_ReadsPlainResponseBody()
    {
        Assert.Equal("93.184.216.34", IpResolver.ParseExternalIpv4("93.184.216.34\n"));
    }

    [Fact]
    public void ParseExternalIpv4_ReadsJsonResponseBody()
    {
        Assert.Equal("1.2.3.4", IpResolver.ParseExternalIpv4("""{"ip": "1.2.3.4"}"""));
    }

    [Fact]
    public void ParseExternalIpv4_TakesTheFirstAddress()
    {
        Assert.Equal("10.0.0.1", IpResolver.ParseExternalIpv4("gateway 10.0.0.1, proxy 10.0.0.2"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no addresses here")]
    [InlineData("999.999.999.999 is not a real address")]  // regex matches, parse rejects
    public void ParseExternalIpv4_ReturnsNull_ForUnusableResponses(string? body)
    {
        Assert.Null(IpResolver.ParseExternalIpv4(body));
    }

    [Fact]
    public void ParseExternalIpv6_ReadsPlainResponseBody()
    {
        Assert.Equal("2a00:1450:4001:81b::200e", IpResolver.ParseExternalIpv6("2a00:1450:4001:81b::200e\n"));
    }

    [Fact]
    public void ParseExternalIpv6_ReturnsNull_ForIpv4OnlyResponses()
    {
        Assert.Null(IpResolver.ParseExternalIpv6("93.184.216.34"));
    }

    [Fact]
    public void PickInternalIpv4_PrefersNonLinkLocalAddresses()
    {
        var linkLocal = IPAddress.Parse("169.254.1.5");
        var lan = IPAddress.Parse("192.168.1.42");

        var picked = IpResolver.PickInternalIpv4([linkLocal, lan]);

        Assert.Equal(lan, picked);
    }

    [Fact]
    public void PickInternalIpv4_ExcludesLoopback()
    {
        var picked = IpResolver.PickInternalIpv4(
            [IPAddress.Loopback, IPAddress.Parse("10.1.2.3")]);

        Assert.Equal(IPAddress.Parse("10.1.2.3"), picked);
    }

    [Fact]
    public void PickInternalIpv4_ReturnsNull_WhenNothingQualifies()
    {
        Assert.Null(IpResolver.PickInternalIpv4(Array.Empty<IPAddress>()));
        Assert.Null(IpResolver.PickInternalIpv4([IPAddress.Loopback]));
    }

    [Fact]
    public void PickInternalIpv6_PrefersGlobalOverLinkLocal()
    {
        var linkLocal = IPAddress.Parse("fe80::1");
        var global = IPAddress.Parse("2a00:1450:4001:81b::200e");

        var picked = IpResolver.PickInternalIpv6([linkLocal, global]);

        Assert.Equal(global, picked);
    }

    [Fact]
    public void PickInternalIpv6_ReturnsLinkLocal_WhenNothingBetterExists()
    {
        var linkLocal = IPAddress.Parse("fe80::42");

        Assert.Equal(linkLocal, IpResolver.PickInternalIpv6([linkLocal]));
    }

    [Fact]
    public void InternalResolvers_RunOnThisMachine()
    {
        // Smoke: never throws, and each family resolves independently
        var v4 = IpResolver.GetInternalIpv4();
        var v6 = IpResolver.GetInternalIpv6();
        Assert.True(v4 == null || IPAddress.Parse(v4).AddressFamily == AddressFamily.InterNetwork);
        Assert.True(v6 == null || IPAddress.Parse(v6).AddressFamily == AddressFamily.InterNetworkV6);
    }
}
