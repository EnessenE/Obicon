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
}
