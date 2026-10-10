using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface IEnrollTokenService
{
    Task<Models.Responses.EnrollTokenResponse> CreateAsync(Models.Requests.CreateEnrollTokenRequest request);
    Task<Models.Page<Models.Responses.EnrollTokenResponse>> GetAsync(Models.Requests.PageParameters page);
    Task<bool> RevokeAsync(Guid id);
    Task<bool> DeleteAsync(Guid id);
    Task<Models.EnrollToken?> FindValidAsync(string plainToken);
}

/// <summary>
/// Manages enroll tokens. Only the SHA-256 hash of a token is stored; the plain
/// value is returned exactly once, at creation.
/// </summary>
public partial class EnrollTokenService : IEnrollTokenService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;

    private readonly ILogger<EnrollTokenService> _logger;

    public EnrollTokenService(IDbContextFactory<ObiconDbContext> dbFactory, ILogger<EnrollTokenService> logger)
    {
        _dbFactory = dbFactory;

        _logger = logger;
    }

    public async Task<EnrollTokenResponse> CreateAsync(CreateEnrollTokenRequest request)
    {
        var plainToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var token = new EnrollToken
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? $"enroll-token-{DateTime.UtcNow:dd-MM-yyyy-HH-mm-ss}"
                : request.Name.Trim(),
            TokenHash = Hash(plainToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            RevokedAt = null,
            PoolId = request.PoolId
        };

        await _dbFactory.ExecuteAsync(async db =>
        {
            if (request.PoolId is { } poolId && !await db.NodePools.AnyAsync(p => p.Id == poolId))
            {
                throw new ArgumentException($"Unknown pool ID: {poolId}");
            }

            db.EnrollTokens.Add(token);
            await db.SaveChangesAsync();
        });

        LogCreatedEnrollToken(token.Id, token.Name,
            token.PoolId is { } pid ? $" scoped to pool {pid}" : string.Empty);
        Metrics.ServerMetrics.Action("created_enroll_token");

        var response = EnrollTokenResponse.From(token);
        response.Token = plainToken;
        return response;
    }

    public async Task<Page<EnrollTokenResponse>> GetAsync(PageParameters page)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var total = await db.EnrollTokens.CountAsync();
        var tokens = await db.EnrollTokens
            .OrderByDescending(t => t.CreatedAt)
            .Skip(page.Offset)
            .Take(page.Limit)
            .ToListAsync();
        return new Page<EnrollTokenResponse>(tokens.Select(EnrollTokenResponse.From).ToList(), total, page.Limit, page.Offset);
    }

    public async Task<bool> RevokeAsync(Guid id)
    {
        var revoked = await _dbFactory.ExecuteAsync(async db =>
        {
            var token = await db.EnrollTokens.FindAsync(id);
            if (token == null || token.RevokedAt != null)
            {
                return token != null;
            }

            token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            LogRevokedEnrollToken(token.Id, token.Name);
            Metrics.ServerMetrics.Action("revoked_enroll_token");
            return true;
        });
        return revoked;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var deleted = await _dbFactory.ExecuteAsync(async db =>
        {
            var token = await db.EnrollTokens.FindAsync(id);
            if (token == null)
            {
                return false;
            }

            db.EnrollTokens.Remove(token);
            await db.SaveChangesAsync();

            LogDeletedEnrollToken(token.Id, token.Name);
            Metrics.ServerMetrics.Action("deleted_enroll_token");
            return true;
        });
        return deleted;
    }

    /// <summary>
    /// Finds the active, non-expired, non-revoked token matching the plain value. Null when invalid.
    /// </summary>
    public async Task<EnrollToken?> FindValidAsync(string plainToken)
    {
        var hash = Hash(plainToken);
        await using var db = await _dbFactory.CreateDbContextAsync();
        var token = await db.EnrollTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (token == null || token.RevokedAt != null)
        {
            return null;
        }

        if (token.ExpiresAt is { } expires && expires <= DateTime.UtcNow)
        {
            return null;
        }

        return token;
    }

    private static string Hash(string plainToken)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hash);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created enroll token {TokenId} ({TokenName}){Scope}")]
    private partial void LogCreatedEnrollToken(Guid tokenId, string tokenName, string scope);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked enroll token {TokenId} ({TokenName})")]
    private partial void LogRevokedEnrollToken(Guid tokenId, string tokenName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted enroll token {TokenId} ({TokenName})")]
    private partial void LogDeletedEnrollToken(Guid tokenId, string tokenName);
}
