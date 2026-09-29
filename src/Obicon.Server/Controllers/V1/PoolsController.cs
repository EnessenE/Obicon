using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

[ApiController]
[Route("v1/[controller]")]
public class PoolsController : ControllerBase
{
    private readonly INodePoolService _poolService;

    public PoolsController(INodePoolService poolService)
    {
        _poolService = poolService;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePool([FromBody] CreatePoolRequest request)
    {
        var pool = await _poolService.CreatePoolAsync(request);
        return CreatedAtAction(nameof(GetPool), new { id = pool.Id }, pool);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllPools()
    {
        var pools = await _poolService.GetAllPoolsAsync();
        return Ok(pools);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPool(Guid id)
    {
        var pool = await _poolService.GetPoolAsync(id);
        if (pool == null)
        {
            return NotFound();
        }
        return Ok(pool);
    }

    /// <summary>
    /// Renames a pool.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePool(Guid id, [FromBody] UpdatePoolRequest request)
    {
        var pool = await _poolService.UpdatePoolAsync(id, request);
        if (pool == null)
        {
            return NotFound();
        }
        return Ok(pool);
    }

    /// <summary>
    /// Replaces the pool's member list. Unknown node IDs are rejected with 400.
    /// </summary>
    [HttpPut("{id}/nodes")]
    public async Task<IActionResult> SetPoolMembers(Guid id, [FromBody] PoolMembersRequest request)
    {
        try
        {
            var pool = await _poolService.SetPoolMembersAsync(id, request);
            if (pool == null)
            {
                return NotFound();
            }
            return Ok(pool);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePool(Guid id)
    {
        var deleted = await _poolService.DeletePoolAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}
