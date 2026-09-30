using Obicon.Shared;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Unit tests for the shared version compatibility rules that gate node connections.
/// Supported means: same major and same minor.
/// </summary>
public class ObiconVersionsTests
{
    [Fact]
    public void Parse_StripsBuildMetadata()
    {
        var parsed = ObiconVersions.Parse("0.2.0+abc123");

        Assert.NotNull(parsed);
        Assert.Equal(0, parsed.Major);
        Assert.Equal(2, parsed.Minor);
        Assert.Equal(0, parsed.Build);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    public void Parse_ReturnsNull_ForUnusableInput(string? input)
    {
        Assert.Null(ObiconVersions.Parse(input));
    }

    [Theory]
    [InlineData("0.2.0", "0.2.0", true)]      // exact match
    [InlineData("0.2.5", "0.2.0", true)]      // patch difference is fine
    [InlineData("0.3.0", "0.2.9", false)]     // minor difference is not supported
    [InlineData("1.2.0", "0.2.0", false)]      // major difference is not supported
    [InlineData("0.2.0+meta", "0.2.0", true)]  // build metadata is ignored
    [InlineData("garbage", "0.2.0", false)]   // unparseable server version is not supported
    public void IsSupported_RequiresSameMajorAndMinor(string serverVersion, string supportedVersion, bool expected)
    {
        Assert.Equal(expected, ObiconVersions.IsSupported(serverVersion, supportedVersion));
    }
}
