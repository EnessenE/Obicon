namespace Obicon.Shared;

/// <summary>
/// Version helpers shared by the server and node: parsing assembly version strings
/// and deciding whether a node and server can work together.
/// </summary>
public static class ObiconVersions
{
    /// <summary>
    /// Parses a version string like "0.2.0" or "0.2.0+abcdef" into a <see cref="Version"/>.
    /// Any metadata after a '+' is ignored, e.g. from AssemblyInformationalVersion.
    /// Returns null when the string is not a valid version.
    /// </summary>
    public static Version? Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var trimmed = version.Trim();
        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0)
        {
            trimmed = trimmed[..plusIndex];
        }

        return Version.TryParse(trimmed, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Checks whether a server version is supported by a component built for
    /// <paramref name="supportedVersion"/>. For now the rule is: same major and
    /// same minor, so 0.2.x supports 0.2.y but not 0.3.0.
    /// </summary>
    public static bool IsSupported(string? serverVersion, string? supportedVersion)
    {
        var server = Parse(serverVersion);
        var supported = Parse(supportedVersion);
        if (server == null || supported == null)
        {
            // Unparseable versions cannot be checked; treat them as unsupported
            return false;
        }

        return server.Major == supported.Major && server.Minor == supported.Minor;
    }
}
