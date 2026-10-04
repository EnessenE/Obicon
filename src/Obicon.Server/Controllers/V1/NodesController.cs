using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Node management: creation, updates, and the live connection status of every node.
/// </summary>
[ApiController]
[Route("v1/nodes")]
[Produces("application/json")]
public class NodesController : ControllerBase
{
    private readonly INodeService _nodeService;
    private readonly INodePoolService _poolService;
    private readonly NodeConnectionManager _connectionManager;

    public NodesController(INodeService nodeService, INodePoolService poolService, NodeConnectionManager connectionManager)
    {
        _nodeService = nodeService;
        _poolService = poolService;
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// Returns one page of nodes, oldest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<NodeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNodes([FromQuery] PageParameters page)
    {
        var result = await _nodeService.GetNodesAsync(page);
        return Ok(new PageResponse<NodeResponse>
        {
            Items = result.Items,
            Total = result.Total,
            Limit = result.Limit,
            Offset = result.Offset
        });
    }

    /// <summary>
    /// Returns the live status of every node: its active flag and whether it is currently connected.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(List<NodeStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNodeStatuses()
    {
        var nodes = await _nodeService.GetNodesAsync(new PageParameters { Limit = PageParameters.MaxLimit });
        var connectedIds = _connectionManager.GetAllConnections()
            .Select(c => Guid.TryParse(c.NodeId, out var id) ? id : (Guid?)null)
            .Where(id => id != null)
            .ToHashSet();

        return Ok(nodes.Items.Select(n => NodeStatusResponse.From(n, connectedIds.Contains(n.Id))));
    }

    /// <summary>
    /// Creates a node and returns its authentication token.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(NodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNode([FromBody] CreateNodeRequest request)
    {
        var node = await _nodeService.CreateNodeAsync(request);
        return CreatedAtAction(nameof(GetNode), new { id = node.Id }, node);
    }

    /// <summary>
    /// Returns a single node.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNode(Guid id)
    {
        var node = await _nodeService.GetNodeAsync(id);
        return node == null ? NotFound() : Ok(node);
    }

    /// <summary>
    /// Updates a node's name and labels, optionally regenerating its token.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(NodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateNode(Guid id, [FromBody] UpdateNodeRequest request)
    {
        var node = await _nodeService.UpdateNodeAsync(id, request);
        return node == null ? NotFound() : Ok(node);
    }

    /// <summary>
    /// Returns the pools the node belongs to.
    /// </summary>
    [HttpGet("{id}/pools")]
    [ProducesResponseType(typeof(List<PoolResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPoolsForNode(Guid id)
    {
        var pools = await _poolService.GetPoolsForNodeAsync(id);
        return Ok(pools);
    }

    /// <summary>
    /// Deletes a node, its pool memberships, and its direct test targets.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNode(Guid id)
    {
        return await _nodeService.DeleteNodeAsync(id) ? NoContent() : NotFound();
    }
}
