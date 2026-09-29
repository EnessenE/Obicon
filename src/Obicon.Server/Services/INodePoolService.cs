using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface INodePoolService
{
    Task<PoolResponse> CreatePoolAsync(CreatePoolRequest request);
    Task<IEnumerable<PoolResponse>> GetAllPoolsAsync();
    Task<PoolResponse?> GetPoolAsync(Guid id);
    Task<PoolResponse?> UpdatePoolAsync(Guid id, UpdatePoolRequest request);
    Task<PoolResponse?> SetPoolMembersAsync(Guid id, PoolMembersRequest request);
    Task<bool> DeletePoolAsync(Guid id);
    Task<IEnumerable<PoolResponse>> GetPoolsForNodeAsync(Guid nodeId);
}
