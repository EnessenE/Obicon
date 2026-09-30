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
    /// IP address the server observed on the node's connection. Null if the node never connected.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Operating settings the node reported on its last connection, e.g. MaxConcurrentTests.
    /// Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
