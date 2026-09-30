using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface INodeEnrollmentService
{
    Task<EnrollResponse> EnrollAsync(EnrollRequest request);
}

/// <summary>
/// Registers nodes that enroll themselves with a valid enroll token.
/// Enrolled nodes manage their own name, labels, and pools.
/// </summary>
public class NodeEnrollmentService : INodeEnrollmentService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly IEnrollTokenService _enrollTokenService;
    private readonly IServerSettingsService _settingsService;
    private readonly ILogger<NodeEnrollmentService> _logger;

    public NodeEnrollmentService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        IEnrollTokenService enrollTokenService,
        IServerSettingsService settingsService,
        ILogger<NodeEnrollmentService> logger)
    {
        _dbFactory = dbFactory;
        _enrollTokenService = enrollTokenService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<EnrollResponse> EnrollAsync(EnrollRequest request)
    {
        if (!await _settingsService.GetAsync<bool>("NodeAutoEnrollmentEnabled"))
        {
            throw new InvalidOperationException("Node auto-enrollment is disabled on this server");
        }

        var enrollToken = await _enrollTokenService.FindValidAsync(request.EnrollToken)
            ?? throw new UnauthorizedAccessException("Invalid, revoked, or expired enroll token");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var labels = request.Labels.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
        var poolIds = await ResolveOrCreatePoolsAsync(db, request.Pools);

        // A pool-scoped token always puts the enrolled node into its pool,
        // on top of the pools the node asked for itself
        if (enrollToken.PoolId is { } tokenPoolId && !poolIds.Contains(tokenPoolId))
        {
            poolIds.Add(tokenPoolId);
        }

        Node? node;
        string? plainToken = null;
        if (request.NodeId is { } nodeId)
        {
            node = await db.Nodes.FindAsync(nodeId);
            if (node == null)
            {
                throw new ArgumentException($"Unknown node ID: {nodeId}");
            }
            if (node.EnrollmentType != NodeEnrollmentType.AutoEnrollment)
            {
                throw new InvalidOperationException("Only nodes that enrolled themselves can update via enrollment");
            }

            node.Name = request.NodeName;
            node.Labels = labels;
        }
        else
        {
            plainToken = Guid.NewGuid().ToString();
            node = new Node
            {
                Id = Guid.NewGuid(),
                Name = request.NodeName,
                AuthToken = TokenHasher.Hash(plainToken),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Labels = labels,
                EnrollmentType = NodeEnrollmentType.AutoEnrollment
            };
            db.Nodes.Add(node);
        }

        foreach (var poolId in poolIds)
        {
            var pool = await db.NodePools.FindAsync(poolId);
            if (pool != null && !pool.NodeIds.Contains(node.Id))
            {
                pool.NodeIds = pool.NodeIds.Append(node.Id).ToList();
            }
        }

        await db.SaveChangesAsync();

        _logger.LogInformation("Node {NodeId} ({NodeName}) enrolled via token {TokenName} ({PoolCount} pool(s))",
            node.Id, node.Name, enrollToken.Name, poolIds.Count);
        Metrics.ServerMetrics.Action("node_enrolled");

        return new EnrollResponse
        {
            Id = node.Id,
            Name = node.Name,
            AuthToken = plainToken ?? string.Empty,
            Labels = node.Labels,
            PoolIds = poolIds
        };
    }

    private static async Task<List<Guid>> ResolveOrCreatePoolsAsync(ObiconDbContext db, List<string> poolNames)
    {
        var names = poolNames
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct()
            .ToList();
        if (names.Count == 0)
        {
            return new List<Guid>();
        }

        // Pool names are not indexed, so fetch the matching ones and create the missing ones
        var existing = await db.NodePools.ToListAsync();
        var result = existing.Where(p => names.Contains(p.Name)).Select(p => p.Id).ToList();

        foreach (var name in names.Where(n => existing.All(p => p.Name != n)))
        {
            var pool = new NodePool
            {
                Id = Guid.NewGuid(),
                Name = name,
                NodeIds = new List<Guid>(),
                CreatedAt = DateTime.UtcNow
            };
            db.NodePools.Add(pool);
            result.Add(pool.Id);
        }

        return result;
    }
}
