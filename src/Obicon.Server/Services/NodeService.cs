using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class NodeService : INodeService
{
    private readonly List<Node> _nodes = new();
    private readonly ILogger<NodeService> _logger;

    public NodeService(ILogger<NodeService> logger)
    {
        _logger = logger;
    }

    public Task<NodeResponse> CreateNodeAsync(CreateNodeRequest request)
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

        _nodes.Add(node);

        _logger.LogInformation("Created node {NodeId} with name: {NodeName}", node.Id, node.Name);

        var response = new NodeResponse
        {
            Id = node.Id,
            Name = node.Name,
            AuthToken = node.AuthToken,
            IsActive = node.IsActive,
            CreatedAt = node.CreatedAt,
            LastSeenAt = node.LastSeenAt
        };

        return Task.FromResult(response);
    }

    public Task<IEnumerable<NodeResponse>> GetAllNodesAsync()
    {
        _logger.LogInformation("Getting all nodes");
        
        var responses = _nodes.Select(n => new NodeResponse
        {
            Id = n.Id,
            Name = n.Name,
            AuthToken = n.AuthToken,
            IsActive = n.IsActive,
            CreatedAt = n.CreatedAt,
            LastSeenAt = n.LastSeenAt
        });

        _logger.LogInformation("Returning {Count} nodes", responses.Count());
        return Task.FromResult(responses);
    }

    public Task<NodeResponse?> GetNodeAsync(Guid id)
    {
        _logger.LogInformation("Getting node with id: {NodeId}", id);
        
        var node = _nodes.FirstOrDefault(n => n.Id == id);
        if (node == null)
        {
            _logger.LogWarning("Node with id {NodeId} not found", id);
            return Task.FromResult<NodeResponse?>(null);
        }

        _logger.LogInformation("Found node: {NodeName}", node.Name);
        return Task.FromResult<NodeResponse?>(new NodeResponse
        {
            Id = node.Id,
            Name = node.Name,
            AuthToken = node.AuthToken,
            IsActive = node.IsActive,
            CreatedAt = node.CreatedAt,
            LastSeenAt = node.LastSeenAt
        });
    }

    public Task<NodeResponse?> UpdateNodeAsync(Guid id, string name)
    {
        _logger.LogInformation("Updating node {NodeId} with new name: {NewName}", id, name);
        
        var node = _nodes.FirstOrDefault(n => n.Id == id);
        if (node == null)
        {
            _logger.LogWarning("Cannot update node {NodeId}: not found", id);
            return Task.FromResult<NodeResponse?>(null);
        }

        node.Name = name;
        _logger.LogInformation("Updated node {NodeId} to name: {NewName}", id, name);

        return Task.FromResult<NodeResponse?>(new NodeResponse
        {
            Id = node.Id,
            Name = node.Name,
            AuthToken = node.AuthToken,
            IsActive = node.IsActive,
            CreatedAt = node.CreatedAt,
            LastSeenAt = node.LastSeenAt
        });
    }

    public Task<bool> DeleteNodeAsync(Guid id)
    {
        _logger.LogInformation("Deleting node with id: {NodeId}", id);
        
        var node = _nodes.FirstOrDefault(n => n.Id == id);
        if (node == null)
        {
            _logger.LogWarning("Cannot delete node {NodeId}: not found", id);
            return Task.FromResult(false);
        }

        _nodes.Remove(node);
        _logger.LogInformation("Deleted node {NodeId}", id);
        return Task.FromResult(true);
    }

    public Task<bool> ValidateNodeTokenAsync(string token)
    {
        var exists = _nodes.Any(n => n.AuthToken == token);
        return Task.FromResult(exists);
    }

    public Task<Node?> GetNodeByTokenAsync(string token)
    {
        var node = _nodes.FirstOrDefault(n => n.AuthToken == token);
        return Task.FromResult(node);
    }

    public Task UpdateNodeLastSeenAsync(Guid nodeId)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node != null)
        {
            node.LastSeenAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }
}
