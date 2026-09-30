namespace Obicon.Server.Models.Responses;

/// <summary>
/// An enroll token as exposed by the API. The plain token is only returned once, on creation.
/// </summary>
public class EnrollTokenResponse
{
    /// <summary>
    /// Unique identifier of the token.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the token.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    /// Pool the token is scoped to: enrolled nodes are always added to this pool.
    /// Null for a server-wide token. Default: null.
    /// </summary>
    public Guid? PoolId { get; set; }

    /// <summary>
    /// Plain token. Only returned by the create endpoint, never stored on the server.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Maps an EnrollToken entity to its API response without the plain token.
    /// </summary>
    /// <param name="token">The token entity to map.</param>
    public static EnrollTokenResponse From(Models.EnrollToken token) => new()
    {
        Id = token.Id,
        Name = token.Name,
        CreatedAt = token.CreatedAt,
        ExpiresAt = token.ExpiresAt,
        RevokedAt = token.RevokedAt,
        PoolId = token.PoolId
    };
}
