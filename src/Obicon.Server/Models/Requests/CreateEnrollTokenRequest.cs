namespace Obicon.Server.Models.Requests;

/// <summary>
/// Creates an enroll token nodes can use to register themselves.
/// </summary>
public class CreateEnrollTokenRequest
{
    /// <summary>
    /// Human-readable name for the token. When empty, generated as enroll-token-dd-MM-yyyy-HH-mm-ss.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Timestamp when the token expires. Null if it never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Pool to scope the token to: enrolled nodes are always added to this pool.
    /// Null for a server-wide token. Optional. Default: null.
    /// </summary>
    public Guid? PoolId { get; set; }
}
