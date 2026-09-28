using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

[ApiController]
[Route("v1/[controller]")]
public class NodesController : ControllerBase
{
    private readonly INodeService _nodeService;

    public NodesController(INodeService nodeService)
    {
        _nodeService = nodeService;
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNode(Guid id, [FromBody] string name)
    {
        var node = await _nodeService.UpdateNodeAsync(id, name);
        if (node == null)
        {
            return NotFound();
        }
        return Ok(node);
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
