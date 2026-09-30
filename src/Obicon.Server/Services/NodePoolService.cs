using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class NodePoolService : INodePoolService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly ILogger<NodePoolService> _logger;

    public NodePoolService(IDbContextFactory<ObiconDbContext> dbFactory, ILogger<NodePoolService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<PoolResponse> CreatePoolAsync(CreatePoolRequest request)
    {
        var pool = new NodePool
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            NodeIds = new List<Guid>(),
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.NodePools.Add(pool);
        await db.SaveChangesAsync();

        _logger.LogInformation("Created pool {PoolId} with name {PoolName}", pool.Id, pool.Name);
        Metrics.ServerMetrics.Action("created_pool");
        return PoolResponse.From(pool);
    }

    public async Task<IEnumerable<PoolResponse>> GetAllPoolsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pools = await db.NodePools.OrderBy(p => p.CreatedAt).ToListAsync();
        return pools.Select(PoolResponse.From);
    }

    public async Task<PoolResponse?> GetPoolAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pool = await db.NodePools.FindAsync(id);
        return pool == null ? null : PoolResponse.From(pool);
    }

    public async Task<PoolResponse?> UpdatePoolAsync(Guid id, UpdatePoolRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pool = await db.NodePools.FindAsync(id);
        if (pool == null)
        {
            return null;
        }

        pool.Name = request.Name;
        pool.Description = request.Description ?? string.Empty;
        await db.SaveChangesAsync();

        _logger.LogInformation("Updated pool {PoolId}: name {PoolName}", id, pool.Name);
        return PoolResponse.From(pool);
    }

    public async Task<PoolResponse?> SetPoolMembersAsync(Guid id, PoolMembersRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // NodeIds is a JSON column, so validate membership against the Nodes table first
        var existingIds = await db.Nodes
            .Where(n => request.NodeIds.Contains(n.Id))
            .Select(n => n.Id)
            .ToListAsync();
        if (existingIds.Count != request.NodeIds.Distinct().Count())
        {
            throw new ArgumentException("Pool members include unknown node IDs");
        }

        var pool = await db.NodePools.FindAsync(id);
        if (pool == null)
        {
            return null;
        }

        pool.NodeIds = request.NodeIds.Distinct().ToList();
        await db.SaveChangesAsync();

        _logger.LogInformation("Pool {PoolId} now has {Count} members", id, pool.NodeIds.Count);
        return PoolResponse.From(pool);
    }

    public async Task<bool> DeletePoolAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pool = await db.NodePools.FindAsync(id);
        if (pool == null)
        {
            return false;
        }

        db.NodePools.Remove(pool);
        await db.SaveChangesAsync();

        _logger.LogInformation("Deleted pool {PoolId}", id);
        Metrics.ServerMetrics.Action("deleted_pool");
        return true;
    }

    public async Task<IEnumerable<PoolResponse>> GetPoolsForNodeAsync(Guid nodeId)
    {
        // NodeIds is a JSON column, so membership must be filtered in memory
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pools = await db.NodePools.OrderBy(p => p.CreatedAt).ToListAsync();
        return pools.Where(p => p.NodeIds.Contains(nodeId)).Select(PoolResponse.From);
    }
}
