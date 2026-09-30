namespace Obicon.Server.Models;

public class Node
{
    /// <summary>
    /// Unique identifier for the node. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the node. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the WebSocket authentication token. The plain token is only ever
    /// returned once, at creation, regeneration, or enrollment. Default: empty string.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the node is currently active and connected. Default: true.
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
    /// Free-form labels attached to this node, used later for test targeting. Default: empty list.
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// How this node came to exist: Manual (created by a user) or AutoEnrollment (enrolled itself).
    /// Auto-enrolled nodes manage their own name, labels, and pools. Default: Manual.
    /// </summary>
    public NodeEnrollmentType EnrollmentType { get; set; } = NodeEnrollmentType.Manual;

    /// <summary>
    /// Version of the node software, reported by the node when it connects. Null if it never connected.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// IP address the server observed on the node's WebSocket connection. Null if it never connected.
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
    /// External (public internet) IPv4 address the node reported about itself.
    /// Null when its check has not succeeded yet.
    /// </summary>
    public string? ExternalIpv4 { get; set; }

    /// <summary>
    /// External (public internet) IPv6 address the node reported about itself.
    /// Null when unavailable.
    /// </summary>
    public string? ExternalIpv6 { get; set; }

    /// <summary>
    /// Operating settings the node reported on its last connection, e.g. MaxConcurrentTests.
    /// Keys match the NodeRegistrationMessage field names. Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
