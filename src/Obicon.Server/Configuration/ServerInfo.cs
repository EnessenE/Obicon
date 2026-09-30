namespace Obicon.Server.Configuration;

/// <summary>
/// Version information for the server, embedded at build time from the csproj Version property.
/// </summary>
public static class ServerInfo
{
    /// <summary>
    /// Version of this server build, e.g. "0.2.0". Default: "0.0.0".
    /// </summary>
    public static string Version { get; } =
        typeof(ServerInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
