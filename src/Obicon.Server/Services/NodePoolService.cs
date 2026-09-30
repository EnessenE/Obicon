using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class NodePoolService : INodePoolService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly SqliteWriteQueue _writeQueue;
    private readonly ILogger<NodePoolService> _logger;

    public NodePoolService(IDbContextFactory<ObiconDbContext> dbFactory, SqliteWriteQueue writeQueue, ILogger<NodePoolService> logger)
    {
        _dbFactory = dbFactory;
        _writeQueue = writeQueue;
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

        await _writeQueue.EnqueueAsync(async db =>
        {
            db.NodePools.Add(pool);
            await db.SaveChangesAsync();
        });

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
        var updated = await _writeQueue.EnqueueAsync(async db =>
        {
            var pool = await db.NodePools.FindAsync(id);
            if (pool == null)
            {
                return (NodePool?)null;
            }

            pool.Name = request.Name;
            pool.Description = request.Description ?? string.Empty;
            await db.SaveChangesAsync();

            _logger.LogInformation("Updated pool {PoolId}: name {PoolName}", id, pool.Name);
            return pool;
        });

        return updated == null ? null : PoolResponse.From(updated);
    }

    public async Task<PoolResponse?> SetPoolMembersAsync(Guid id, PoolMembersRequest request)
    {
        var updated = await _writeQueue.EnqueueAsync(async db =>
        {
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
                return (NodePool?)null;
            }

            pool.NodeIds = request.NodeIds.Distinct().ToList();
            await db.SaveChangesAsync();

            _logger.LogInformation("Pool {PoolId} now has {Count} members", id, pool.NodeIds.Count);
            return pool;
        });

        return updated == null ? null : PoolResponse.From(updated);
    }

    public async Task<bool> DeletePoolAsync(Guid id)
    {
        var deleted = await _writeQueue.EnqueueAsync(async db =>
        {
            var pool = await db.NodePools.FindAsync(id);
            if (pool == null)
            {
                return false;
            }

            db.NodePools.Remove(pool);
            await db.SaveChangesAsync();
            return true;
        });

        if (deleted)
        {
            _logger.LogInformation("Deleted pool {PoolId}", id);
            Metrics.ServerMetrics.Action("deleted_pool");
        }
        return deleted;
    }

    public async Task<IEnumerable<PoolResponse>> GetPoolsForNodeAsync(Guid nodeId)
    {
        // NodeIds is a JSON column, so membership must be filtered in memory
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pools = await db.NodePools.OrderBy(p => p.CreatedAt).ToListAsync();
        return pools.Where(p => p.NodeIds.Contains(nodeId)).Select(PoolResponse.From);
    }
}
