namespace Obicon.Server.Models;

/// <summary>
/// A token nodes can use to enroll themselves. Only the SHA-256 hash of the token is stored;
/// the plain value is shown exactly once at creation.
/// </summary>
public class EnrollToken
{
    /// <summary>
    /// Unique identifier of the token. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the token. Generated as enroll-token-dd-MM-yyyy-HH-mm-ss when not given.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the plain token, hex encoded. The plain token is never stored.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the token was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the token expires. Null if it never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when the token was revoked. Null while the token is active.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Pool this token is scoped to: enrolled nodes are always added to this pool.
    /// Null for a server-wide token that lets nodes choose their own pools. Default: null.
    /// </summary>
    public Guid? PoolId { get; set; }
}
