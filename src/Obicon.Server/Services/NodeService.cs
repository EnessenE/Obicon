using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class NodeService : INodeService
{
    private readonly List<Node> _nodes = new();

    public Task<NodeResponse> CreateNodeAsync(CreateNodeRequest request)
    {
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
        var responses = _nodes.Select(n => new NodeResponse
        {
            Id = n.Id,
            Name = n.Name,
            AuthToken = n.AuthToken,
            IsActive = n.IsActive,
            CreatedAt = n.CreatedAt,
            LastSeenAt = n.LastSeenAt
        });

        return Task.FromResult(responses);
    }

    public Task<NodeResponse?> GetNodeAsync(Guid id)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == id);
        if (node == null)
            return Task.FromResult<NodeResponse?>(null);

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
        var node = _nodes.FirstOrDefault(n => n.Id == id);
        if (node == null)
            return Task.FromResult<NodeResponse?>(null);

        node.Name = name;

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
        var node = _nodes.FirstOrDefault(n => n.Id == id);
        if (node == null)
            return Task.FromResult(false);

        _nodes.Remove(node);
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
