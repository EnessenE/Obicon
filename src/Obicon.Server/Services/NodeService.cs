using Microsoft.EntityFrameworkCore;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.WebSockets;
using Obicon.Shared;

namespace Obicon.Server.Services;

public partial class NodeService : INodeService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;

    private readonly NodeConnectionManager _connectionManager;
    private readonly ILogger<NodeService> _logger;

    public NodeService(IDbContextFactory<ObiconDbContext> dbFactory, NodeConnectionManager connectionManager, ILogger<NodeService> logger)
    {
        _dbFactory = dbFactory;

        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task<NodeResponse> CreateNodeAsync(CreateNodeRequest request)
    {
        LogCreatingNode(request.Name);

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

        var response = await _dbFactory.ExecuteAsync(async db =>
        {
            db.Nodes.Add(node);
            await db.SaveChangesAsync();
            return ToResponse(node);
        });

        LogCreatedNode(node.Id, node.Name);
        Metrics.ServerMetrics.Action("created_node");

        response.AuthToken = plainToken;
        return response;
    }

    /// <summary>
    /// Number of nodes registered, for the stats endpoint; avoids loading a page.
    /// </summary>
    public async Task<int> GetNodeCountAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Nodes.CountAsync();
    }

    public async Task<Page<NodeResponse>> GetNodesAsync(PageParameters page)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var total = await db.Nodes.CountAsync();
        var nodes = await db.Nodes
            .Include(n => n.Labels)
            .Include(n => n.Settings)
            .OrderBy(n => n.CreatedAt)
            .Skip(page.Offset)
            .Take(page.Limit)
            .ToListAsync();
        return new Page<NodeResponse>(nodes.Select(ToResponse).ToList(), total, page.Limit, page.Offset);
    }

    public async Task<NodeResponse?> GetNodeAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var node = await db.Nodes
            .Include(n => n.Labels)
            .Include(n => n.Settings)
            .FirstOrDefaultAsync(n => n.Id == id);
        if (node == null)
        {
            LogNodeNotFound(id);
            return null;
        }
        return ToResponse(node);
    }

    public async Task<NodeResponse?> UpdateNodeAsync(Guid id, UpdateNodeRequest request)
    {
        var (response, plainToken) = await _dbFactory.ExecuteAsync(async db =>
        {
            var node = await db.Nodes
                .Include(n => n.Labels)
                .Include(n => n.Settings)
                .FirstOrDefaultAsync(n => n.Id == id);
            if (node == null)
            {
                LogCannotUpdateNode(id);
                return ((NodeResponse?)null, (string?)null);
            }

            if (node.EnrollmentType == NodeEnrollmentType.AutoEnrollment)
            {
                throw new InvalidOperationException($"Node {id} auto-enrolled; its name, labels, and pools are managed by the node itself");
            }

            node.Name = request.Name;
            ReplaceLabels(db, node, request.Labels);

            string? plainToken = null;
            if (request.RegenerateToken)
            {
                plainToken = Guid.NewGuid().ToString();
                node.AuthToken = TokenHasher.Hash(plainToken);
                LogRegeneratedToken(id);
                Metrics.ServerMetrics.Action("token_regenerated");
            }

            await db.SaveChangesAsync();
            return (ToResponse(node), plainToken);
        });

        if (response == null)
        {
            return null;
        }

        LogUpdatedNode(id, request.Name);

        if (plainToken != null)
        {
            response.AuthToken = plainToken;
        }
        return response;
    }

    public async Task<bool> DeleteNodeAsync(Guid id)
    {
        var deleted = await _dbFactory.ExecuteAsync(async db =>
        {
            var node = await db.Nodes.FindAsync(id);
            if (node == null)
            {
                LogCannotDeleteNode(id);
                return false;
            }

            // The join rows (labels, reported settings, pool membership, direct test
            // targets) go with the node through their cascading foreign keys, so a
            // deleted node leaves no stale reference that keeps queueing jobs
            db.Nodes.Remove(node);
            await db.SaveChangesAsync();
            return true;
        });

        if (deleted)
        {
            // The node record is gone: close its live connection too, otherwise a
            // deleted node keeps heartbeating as a ghost that no longer shows in the list
            await _connectionManager.DisconnectNodeAsync(id.ToString(), "Node was deleted");
            LogDeletedNode(id);
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
        return _dbFactory.ExecuteAsync(async db =>
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
        return _dbFactory.ExecuteAsync(async db =>
        {
            var node = await db.Nodes
                .Include(n => n.Settings)
                .FirstOrDefaultAsync(n => n.Id == nodeId);
            if (node == null)
            {
                return;
            }

            var reportedSettings = settings ?? new Dictionary<string, string>();
            var storedSettings = node.Settings.OrderBy(s => s.Key, StringComparer.Ordinal)
                .Select(s => (s.Key, s.Value))
                .ToList();
            var incomingSettings = reportedSettings.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
            var changed = node.Version != version ||
                          node.IpAddress != ipAddress ||
                          !storedSettings.SequenceEqual(incomingSettings);
            if (!changed)
            {
                return;
            }

            node.Version = version;
            node.IpAddress = ipAddress;
            db.NodeReportedSettings.RemoveRange(node.Settings);
            db.NodeReportedSettings.AddRange(reportedSettings.Select(kv => new NodeReportedSetting
            {
                NodeId = node.Id,
                Key = kv.Key,
                Value = kv.Value
            }));
            await db.SaveChangesAsync();

            LogConnectionInfoUpdated(nodeId, version, ipAddress);
        });
    }

    public Task UpdateNodeReportedAddressesAsync(Guid nodeId, string? internalIpv4, string? internalIpv6, string? externalIpv4, string? externalIpv6)
    {
        return _dbFactory.ExecuteAsync(async db =>
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

            LogAddressesUpdated(nodeId,
                internalIpv4 ?? "unavailable", internalIpv6 ?? "unavailable",
                externalIpv4 ?? "unavailable", externalIpv6 ?? "unavailable");
        });
    }

    /// <summary>
    /// Replaces the node's label rows with the given list, inside the caller's unit of work.
    /// </summary>
    private static void ReplaceLabels(ObiconDbContext db, Node node, List<string> labels)
    {
        db.NodeLabels.RemoveRange(node.Labels);
        node.Labels = labels
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(l => new NodeLabel { NodeId = node.Id, Label = l })
            .ToList();
        db.NodeLabels.AddRange(node.Labels);
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
        Labels = node.Labels.Select(l => l.Label).ToList(),
        EnrollmentType = node.EnrollmentType == NodeEnrollmentType.AutoEnrollment ? "auto-enrollment" : "manual",
        Version = node.Version,
        VersionSupported = node.Version == null
            ? null
            : ObiconVersions.IsSupported(ServerInfo.Version, node.Version),
        IpAddress = node.IpAddress,
        InternalIpv4 = node.InternalIpv4,
        InternalIpv6 = node.InternalIpv6,
        ExternalIpv4 = node.ExternalIpv4,
        ExternalIpv6 = node.ExternalIpv6,
        Settings = node.Settings.ToDictionary(s => s.Key, s => s.Value)
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating a node with name: {NodeName}")]
    private partial void LogCreatingNode(string nodeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created node {NodeId} with name: {NodeName}")]
    private partial void LogCreatedNode(Guid nodeId, string nodeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Node with id {NodeId} not found")]
    private partial void LogNodeNotFound(Guid nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot update node {NodeId}: not found")]
    private partial void LogCannotUpdateNode(Guid nodeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Regenerated auth token for node {NodeId}")]
    private partial void LogRegeneratedToken(Guid nodeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated node {NodeId} to name: {NewName}")]
    private partial void LogUpdatedNode(Guid nodeId, string newName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot delete node {NodeId}: not found")]
    private partial void LogCannotDeleteNode(Guid nodeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted node {NodeId}")]
    private partial void LogDeletedNode(Guid nodeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Node {NodeId} connection info updated: version={Version} ip={IpAddress}")]
    private partial void LogConnectionInfoUpdated(Guid nodeId, string? version, string? ipAddress);

    [LoggerMessage(Level = LogLevel.Information, Message = "Node {NodeId} addresses updated: int4={InternalIpv4} int6={InternalIpv6} ext4={ExternalIpv4} ext6={ExternalIpv6}")]
    private partial void LogAddressesUpdated(Guid nodeId, string internalIpv4, string internalIpv6, string externalIpv4, string externalIpv6);
}
