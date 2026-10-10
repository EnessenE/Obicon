using System.Net;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface INodeService
{
    Task<NodeResponse> CreateNodeAsync(CreateNodeRequest request);
    Task<Page<NodeResponse>> GetNodesAsync(PageParameters page);
    /// <summary>
    /// Number of nodes registered, for the stats endpoint; avoids loading a page.
    /// </summary>
    Task<int> GetNodeCountAsync();
    Task<NodeResponse?> GetNodeAsync(Guid id);
    Task<NodeResponse?> UpdateNodeAsync(Guid id, Models.Requests.UpdateNodeRequest request);
    Task<bool> DeleteNodeAsync(Guid id);
    Task<bool> ValidateNodeTokenAsync(string token);
    Task<Node?> GetNodeByTokenAsync(string token);
    Task UpdateNodeLastSeenAsync(Guid nodeId);
    Task UpdateNodeConnectionInfoAsync(Guid nodeId, string? version, IPAddress? ipAddress, Dictionary<string, string>? settings);
    Task UpdateNodeReportedAddressesAsync(Guid nodeId, IPAddress? internalIpv4, IPAddress? internalIpv6, IPAddress? externalIpv4, IPAddress? externalIpv6);
}
