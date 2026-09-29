using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;

namespace Obicon.Server.Controllers.V1;

[ApiController]
[Route("v1/[controller]")]
public class NodesController : ControllerBase
{
    private readonly INodeService _nodeService;
    private readonly INodePoolService _poolService;
    private readonly NodeConnectionManager _connectionManager;

    public NodesController(
        INodeService nodeService,
        INodePoolService poolService,
        NodeConnectionManager connectionManager)
    {
        _nodeService = nodeService;
        _poolService = poolService;
        _connectionManager = connectionManager;
    }

    [HttpPost]
    public async Task<IActionResult> CreateNode([FromBody] CreateNodeRequest request)
    {
        var node = await _nodeService.CreateNodeAsync(request);
        return CreatedAtAction(nameof(GetNode), new { id = node.Id }, node);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllNodes()
    {
        var nodes = await _nodeService.GetAllNodesAsync();
        return Ok(nodes);
    }

    /// <summary>
    /// Returns the live status of every node: active flag and current WebSocket connectivity.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetNodeStatuses()
    {
        var nodes = await _nodeService.GetAllNodesAsync();
        var connectedIds = _connectionManager.GetAllConnections()
            .Select(c => Guid.TryParse(c.NodeId, out var id) ? id : (Guid?)null)
            .Where(id => id != null)
            .ToHashSet();

        return Ok(nodes.Select(n => NodeStatusResponse.From(n, connectedIds.Contains(n.Id))));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNode(Guid id)
    {
        var node = await _nodeService.GetNodeAsync(id);
        if (node == null)
        {
            return NotFound();
        }
        return Ok(node);
    }

    /// <summary>
    /// Updates a node's name and labels, optionally regenerating its auth token.
    /// A regenerated token expires immediately and closes any live connection using the old token.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNode(Guid id, [FromBody] UpdateNodeRequest request)
    {
        var node = await _nodeService.UpdateNodeAsync(id, request);
        if (node == null)
        {
            return NotFound();
        }

        if (request.RegenerateToken)
        {
            await _connectionManager.DisconnectNodeAsync(id.ToString());
        }

        return Ok(node);
    }

    /// <summary>
    /// Returns all pools this node belongs to.
    /// </summary>
    [HttpGet("{id}/pools")]
    public async Task<IActionResult> GetPoolsForNode(Guid id)
    {
        if (await _nodeService.GetNodeAsync(id) == null)
        {
            return NotFound();
        }

        var pools = await _poolService.GetPoolsForNodeAsync(id);
        return Ok(pools);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNode(Guid id)
    {
        var deleted = await _nodeService.DeleteNodeAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}
