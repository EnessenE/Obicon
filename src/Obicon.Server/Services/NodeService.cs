using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.WebSockets;

namespace Obicon.Server.Services;

public class NodeService : INodeService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly SqliteWriteQueue _writeQueue;
    private readonly NodeConnectionManager _connectionManager;
    private readonly ILogger<NodeService> _logger;

    public NodeService(IDbContextFactory<ObiconDbContext> dbFactory, SqliteWriteQueue writeQueue, NodeConnectionManager connectionManager, ILogger<NodeService> logger)
    {
        _dbFactory = dbFactory;
        _writeQueue = writeQueue;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task<NodeResponse> CreateNodeAsync(CreateNodeRequest request)
    {
        _logger.LogInformation("Creating a node with name: {NodeName}", request.Name);

        // Only the hash of the auth token is stored; the plain value is returned once
        var plainToken = Guid.NewGuid().ToString();
        var node = new Node
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            AuthToken = TokenHasher.Hash(plainToken),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = null
        };

        var response = await _writeQueue.EnqueueAsync(async db =>
        {
            db.Nodes.Add(node);
            await db.SaveChangesAsync();
            return ToResponse(node);
        });

        _logger.LogInformation("Created node {NodeId} with name: {NodeName}", node.Id, node.Name);
        Metrics.ServerMetrics.Action("created_node");

        response.AuthToken = plainToken;
        return response;
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
        var (response, plainToken) = await _writeQueue.EnqueueAsync(async db =>
        {
            var node = await db.Nodes.FindAsync(id);
            if (node == null)
            {
                _logger.LogWarning("Cannot update node {NodeId}: not found", id);
                return ((NodeResponse?)null, (string?)null);
            }

            if (node.EnrollmentType == NodeEnrollmentType.AutoEnrollment)
            {
                throw new InvalidOperationException($"Node {id} auto-enrolled; its name, labels, and pools are managed by the node itself");
            }

            node.Name = request.Name;
            node.Labels = request.Labels;

            string? plainToken = null;
            if (request.RegenerateToken)
            {
                plainToken = Guid.NewGuid().ToString();
                node.AuthToken = TokenHasher.Hash(plainToken);
                _logger.LogInformation("Regenerated auth token for node {NodeId}", id);
                Metrics.ServerMetrics.Action("token_regenerated");
            }

            await db.SaveChangesAsync();
            return (ToResponse(node), plainToken);
        });

        if (response == null)
        {
            return null;
        }

        _logger.LogInformation("Updated node {NodeId} to name: {NewName}", id, request.Name);

        if (plainToken != null)
        {
            response.AuthToken = plainToken;
        }
        return response;
    }

    public async Task<bool> DeleteNodeAsync(Guid id)
    {
        var deleted = await _writeQueue.EnqueueAsync(async db =>
        {
            var node = await db.Nodes.FindAsync(id);
            if (node == null)
            {
                _logger.LogWarning("Cannot delete node {NodeId}: not found", id);
                return false;
            }

            db.Nodes.Remove(node);

            // Drop the node from every pool; NodeIds is a JSON column, so a new
            // list is assigned for EF's change tracker to see the change
            var memberPools = (await db.NodePools.ToListAsync())
                .Where(p => p.NodeIds.Contains(id)).ToList();
            foreach (var pool in memberPools)
            {
                pool.NodeIds = pool.NodeIds.Where(nodeId => nodeId != id).ToList();
            }

            await db.SaveChangesAsync();
            return true;
        });

        if (deleted)
        {
            // The node record is gone: close its live connection too, otherwise a
            // deleted node keeps heartbeating as a ghost that no longer shows in the list
            await _connectionManager.DisconnectNodeAsync(id.ToString(), "Node was deleted");
            _logger.LogInformation("Deleted node {NodeId}", id);
            Metrics.ServerMetrics.Action("deleted_node");
        }
        return deleted;
    }

    public async Task<bool> ValidateNodeTokenAsync(string token)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var hash = TokenHasher.Hash(token);
        return await db.Nodes.AnyAsync(n => n.AuthToken == hash);
    }

    public async Task<Node?> GetNodeByTokenAsync(string token)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var hash = TokenHasher.Hash(token);
        return await db.Nodes.FirstOrDefaultAsync(n => n.AuthToken == hash);
    }

    public Task UpdateNodeLastSeenAsync(Guid nodeId)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
            var node = await db.Nodes.FindAsync(nodeId);
            if (node != null)
            {
                node.LastSeenAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        });
    }

    public Task UpdateNodeConnectionInfoAsync(Guid nodeId, string? version, string? ipAddress, Dictionary<string, string>? settings)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
            var node = await db.Nodes.FindAsync(nodeId);
            if (node == null)
            {
                return;
            }

            var reportedSettings = settings ?? new Dictionary<string, string>();
            var changed = node.Version != version ||
                          node.IpAddress != ipAddress ||
                          !node.Settings.OrderBy(kv => kv.Key).SequenceEqual(reportedSettings.OrderBy(kv => kv.Key));
            if (!changed)
            {
                return;
            }

            node.Version = version;
            node.IpAddress = ipAddress;
            node.Settings = reportedSettings;
            await db.SaveChangesAsync();

            _logger.LogInformation("Node {NodeId} connection info updated: version={Version} ip={IpAddress}", nodeId, version, ipAddress);
        });
    }

    public Task UpdateNodeReportedAddressesAsync(Guid nodeId, string? internalIpv4, string? internalIpv6, string? externalIpv4, string? externalIpv6)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
            var node = await db.Nodes.FindAsync(nodeId);
            if (node == null ||
                (node.InternalIpv4 == internalIpv4 && node.InternalIpv6 == internalIpv6 &&
                 node.ExternalIpv4 == externalIpv4 && node.ExternalIpv6 == externalIpv6))
            {
                return;
            }

            node.InternalIpv4 = internalIpv4;
            node.InternalIpv6 = internalIpv6;
            node.ExternalIpv4 = externalIpv4;
            node.ExternalIpv6 = externalIpv6;
            await db.SaveChangesAsync();

            _logger.LogInformation("Node {NodeId} addresses updated: int4={InternalIpv4} int6={InternalIpv6} ext4={ExternalIpv4} ext6={ExternalIpv6}",
                nodeId, internalIpv4 ?? "unavailable", internalIpv6 ?? "unavailable",
                externalIpv4 ?? "unavailable", externalIpv6 ?? "unavailable");
        });
    }

    private static NodeResponse ToResponse(Node node) => new()
    {
        Id = node.Id,
        Name = node.Name,
        // The stored value is a hash; the plain token is only set by the flows that issue it
        AuthToken = string.Empty,
        IsActive = node.IsActive,
        CreatedAt = node.CreatedAt,
        LastSeenAt = node.LastSeenAt,
        Labels = node.Labels,
        EnrollmentType = node.EnrollmentType == NodeEnrollmentType.AutoEnrollment ? "auto-enrollment" : "manual",
        Version = node.Version,
        IpAddress = node.IpAddress,
        InternalIpv4 = node.InternalIpv4,
        InternalIpv6 = node.InternalIpv6,
        ExternalIpv4 = node.ExternalIpv4,
        ExternalIpv6 = node.ExternalIpv6,
        Settings = node.Settings
    };
}
