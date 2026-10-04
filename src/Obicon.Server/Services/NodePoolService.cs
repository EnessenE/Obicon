using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public partial class NodePoolService : INodePoolService
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
            Members = new List<PoolMember>(),
            CreatedAt = DateTime.UtcNow
        };

        await _dbFactory.ExecuteAsync(async db =>
        {
            db.NodePools.Add(pool);
            await db.SaveChangesAsync();
        });

        LogCreatedPool(pool.Id, pool.Name);
        Metrics.ServerMetrics.Action("created_pool");
        return PoolResponse.From(pool);
    }

    public async Task<IEnumerable<PoolResponse>> GetAllPoolsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pools = await db.NodePools
            .Include(p => p.Members)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
        return pools.Select(PoolResponse.From);
    }

    public async Task<PoolResponse?> GetPoolAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pool = await db.NodePools
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        return pool == null ? null : PoolResponse.From(pool);
    }

    public async Task<PoolResponse?> UpdatePoolAsync(Guid id, UpdatePoolRequest request)
    {
        var updated = await _dbFactory.ExecuteAsync(async db =>
        {
            var pool = await db.NodePools
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pool == null)
            {
                return (NodePool?)null;
            }

            pool.Name = request.Name;
            pool.Description = request.Description ?? string.Empty;
            await db.SaveChangesAsync();

            LogUpdatedPool(id, pool.Name);
            return pool;
        });

        return updated == null ? null : PoolResponse.From(updated);
    }

    public async Task<PoolResponse?> SetPoolMembersAsync(Guid id, PoolMembersRequest request)
    {
        var updated = await _dbFactory.ExecuteAsync(async db =>
        {
            var memberIds = request.NodeIds.Distinct().ToList();
            var existingIds = await db.Nodes
                .Where(n => memberIds.Contains(n.Id))
                .Select(n => n.Id)
                .ToListAsync();
            if (existingIds.Count != memberIds.Count)
            {
                throw new ArgumentException("Pool members include unknown node IDs");
            }

            var pool = await db.NodePools
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pool == null)
            {
                return (NodePool?)null;
            }

            db.PoolMembers.RemoveRange(pool.Members);
            pool.Members = memberIds.Select(nodeId => new PoolMember { PoolId = pool.Id, NodeId = nodeId }).ToList();
            db.PoolMembers.AddRange(pool.Members);
            await db.SaveChangesAsync();

            LogPoolMembersSet(id, pool.Members.Count);
            return pool;
        });

        return updated == null ? null : PoolResponse.From(updated);
    }

    public async Task<bool> DeletePoolAsync(Guid id)
    {
        var deleted = await _dbFactory.ExecuteAsync(async db =>
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
            LogDeletedPool(id);
            Metrics.ServerMetrics.Action("deleted_pool");
        }
        return deleted;
    }

    public async Task<IEnumerable<PoolResponse>> GetPoolsForNodeAsync(Guid nodeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pools = await db.NodePools
            .Include(p => p.Members)
            .OrderBy(p => p.CreatedAt)
            .Where(p => p.Members.Any(m => m.NodeId == nodeId))
            .ToListAsync();
        return pools.Select(PoolResponse.From);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created pool {PoolId} with name {PoolName}")]
    private partial void LogCreatedPool(Guid poolId, string poolName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated pool {PoolId}: name {PoolName}")]
    private partial void LogUpdatedPool(Guid poolId, string poolName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pool {PoolId} now has {Count} members")]
    private partial void LogPoolMembersSet(Guid poolId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted pool {PoolId}")]
    private partial void LogDeletedPool(Guid poolId);
}
