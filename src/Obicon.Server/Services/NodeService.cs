using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class NodeService : INodeService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly ILogger<NodeService> _logger;

    public NodeService(IDbContextFactory<ObiconDbContext> dbFactory, ILogger<NodeService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<NodeResponse> CreateNodeAsync(CreateNodeRequest request)
    {
        _logger.LogInformation("Creating a node with name: {NodeName}", request.Name);

        var node = new Node
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            AuthToken = Guid.NewGuid().ToString(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = null
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Nodes.Add(node);
        await db.SaveChangesAsync();

        _logger.LogInformation("Created node {NodeId} with name: {NodeName}", node.Id, node.Name);
        Metrics.ServerMetrics.Action("created_node");
        return ToResponse(node);
    }

    public async Task<IEnumerable<NodeResponse>> GetAllNodesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var nodes = await db.Nodes.OrderBy(n => n.CreatedAt).ToListAsync();
        return nodes.Select(ToResponse);
    }

    public async Task<NodeResponse?> GetNodeAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var node = await db.Nodes.FindAsync(id);
        if (node == null)
        {
            _logger.LogWarning("Node with id {NodeId} not found", id);
            return null;
        }
        return ToResponse(node);
    }

    public async Task<NodeResponse?> UpdateNodeAsync(Guid id, UpdateNodeRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var node = await db.Nodes.FindAsync(id);
        if (node == null)
        {
            _logger.LogWarning("Cannot update node {NodeId}: not found", id);
            return null;
        }

        node.Name = request.Name;
        node.Labels = request.Labels;

        if (request.RegenerateToken)
        {
            node.AuthToken = Guid.NewGuid().ToString();
            _logger.LogInformation("Regenerated auth token for node {NodeId}", id);
            Metrics.ServerMetrics.Action("token_regenerated");
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Updated node {NodeId} to name: {NewName}", id, request.Name);
        return ToResponse(node);
    }

    public async Task<bool> DeleteNodeAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var node = await db.Nodes.FindAsync(id);
        if (node == null)
        {
            _logger.LogWarning("Cannot delete node {NodeId}: not found", id);
            return false;
        }

        db.Nodes.Remove(node);
        await db.SaveChangesAsync();
        _logger.LogInformation("Deleted node {NodeId}", id);
        Metrics.ServerMetrics.Action("deleted_node");
        return true;
    }

    public async Task<bool> ValidateNodeTokenAsync(string token)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Nodes.AnyAsync(n => n.AuthToken == token);
    }

    public async Task<Node?> GetNodeByTokenAsync(string token)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Nodes.FirstOrDefaultAsync(n => n.AuthToken == token);
    }

    public async Task UpdateNodeLastSeenAsync(Guid nodeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var node = await db.Nodes.FindAsync(nodeId);
        if (node != null)
        {
            node.LastSeenAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private static NodeResponse ToResponse(Node node) => new()
    {
        Id = node.Id,
        Name = node.Name,
        AuthToken = node.AuthToken,
        IsActive = node.IsActive,
        CreatedAt = node.CreatedAt,
        LastSeenAt = node.LastSeenAt,
        Labels = node.Labels
    };
}
