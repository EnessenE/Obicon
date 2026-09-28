using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface INodeService
{
    Task<NodeResponse> CreateNodeAsync(CreateNodeRequest request);
    Task<IEnumerable<NodeResponse>> GetAllNodesAsync();
    Task<NodeResponse?> GetNodeAsync(Guid id);
    Task<NodeResponse?> UpdateNodeAsync(Guid id, string name);
    Task<bool> DeleteNodeAsync(Guid id);
    Task<bool> ValidateNodeTokenAsync(string token);
    Task<Node?> GetNodeByTokenAsync(string token);
    Task UpdateNodeLastSeenAsync(Guid nodeId);
}
