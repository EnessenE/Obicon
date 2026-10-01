namespace Obicon.Server.Models.Responses;

public class NodeResponse
{
    /// <summary>
    /// Unique identifier for the node.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the node. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plain authentication token, only filled on creation, regeneration, or enrollment.
    /// Default: empty string.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the node is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when the node was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last heartbeat received from the node. Null if never connected.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// Free-form labels attached to this node. Default: empty list.
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// How this node came to exist: "manual" or "auto-enrollment".
    /// </summary>
    public string EnrollmentType { get; set; } = "manual";

    /// <summary>
    /// Version of the node software, reported on connection. Null if the node never connected.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Whether the reported version is inside the server's supported range (same major.minor),
    /// as checked on the node's connection. Null if the node never reported a version.
    /// </summary>
    public bool? VersionSupported { get; set; }

    /// <summary>
    /// IP address the server observed on the node's connection. Null if the node never connected.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Internal (LAN) IPv4 address the node reported about itself. Null when unavailable.
    /// </summary>
    public string? InternalIpv4 { get; set; }

    /// <summary>
    /// Internal (LAN) IPv6 address the node reported about itself. Null when unavailable.
    /// </summary>
    public string? InternalIpv6 { get; set; }

    /// <summary>
    /// External (public internet) IPv4 address the node reported about itself. Null when not reported.
    /// </summary>
    public string? ExternalIpv4 { get; set; }

    /// <summary>
    /// External (public internet) IPv6 address the node reported about itself. Null when unavailable.
    /// </summary>
    public string? ExternalIpv6 { get; set; }

    /// <summary>
    /// Operating settings the node reported on its last connection, e.g. MaxConcurrentTests.
    /// Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
