namespace Obicon.Node.Configuration;

/// <summary>
/// Version information for the node, embedded at build time from the csproj Version property.
/// The node supports servers within the same major.minor version.
/// </summary>
public static class NodeInfo
{
    /// <summary>
    /// Version of this node build, e.g. "0.2.0". Default: "0.0.0".
    /// </summary>
    public static string Version { get; } =
        typeof(NodeInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
