using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Node pool management: grouping nodes and replacing a pool's member list.
/// </summary>
[ApiController]
[Route("v1/pools")]
[Produces("application/json")]
public class PoolsController : ControllerBase
{
    private readonly INodePoolService _poolService;

    public PoolsController(INodePoolService poolService)
    {
        _poolService = poolService;
    }

    /// <summary>
    /// Returns one page of pools, oldest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<PoolResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPools([FromQuery] PageParameters page)
    {
        var result = await _poolService.GetPoolsAsync(page);
        return Ok(new PageResponse<PoolResponse>
        {
            Items = result.Items,
            Total = result.Total,
            Limit = result.Limit,
            Offset = result.Offset
        });
    }

    /// <summary>
    /// Creates a pool.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PoolResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePool([FromBody] CreatePoolRequest request)
    {
        var pool = await _poolService.CreatePoolAsync(request);
        return CreatedAtAction(nameof(GetPool), new { id = pool.Id }, pool);
    }

    /// <summary>
    /// Returns a single pool.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PoolResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPool(Guid id)
    {
        var pool = await _poolService.GetPoolAsync(id);
        return pool == null ? NotFound() : Ok(pool);
    }

    /// <summary>
    /// Updates a pool's name and description.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PoolResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePool(Guid id, [FromBody] UpdatePoolRequest request)
    {
        var pool = await _poolService.UpdatePoolAsync(id, request);
        return pool == null ? NotFound() : Ok(pool);
    }

    /// <summary>
    /// Replaces the pool's member list.
    /// </summary>
    [HttpPut("{id}/nodes")]
    [ProducesResponseType(typeof(PoolResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPoolMembers(Guid id, [FromBody] PoolMembersRequest request)
    {
        var pool = await _poolService.SetPoolMembersAsync(id, request);
        return pool == null ? NotFound() : Ok(pool);
    }

    /// <summary>
    /// Deletes the pool; its member nodes are not affected.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePool(Guid id)
    {
        return await _poolService.DeletePoolAsync(id) ? NoContent() : NotFound();
    }
}
