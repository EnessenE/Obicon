using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Enroll token management: creating, listing, revoking, and deleting the tokens
/// nodes use to enroll themselves. Plain tokens are returned exactly once, at creation.
/// </summary>
[ApiController]
[Route("v1/enroll-tokens")]
[Produces("application/json")]
public class EnrollTokensController : ControllerBase
{
    private readonly IEnrollTokenService _enrollTokenService;

    public EnrollTokensController(IEnrollTokenService enrollTokenService)
    {
        _enrollTokenService = enrollTokenService;
    }

    /// <summary>
    /// Creates an enroll token. The plain token is returned exactly once; only its hash is stored.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EnrollTokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEnrollTokenRequest request)
    {
        var token = await _enrollTokenService.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = token.Id }, token);
    }

    /// <summary>
    /// Returns one page of enroll tokens with their state. Plain tokens are never included.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<EnrollTokenResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] PageParameters page)
    {
        var result = await _enrollTokenService.GetAsync(page);
        return Ok(new PageResponse<EnrollTokenResponse>
        {
            Items = result.Items,
            Total = result.Total,
            Limit = result.Limit,
            Offset = result.Offset
        });
    }

    /// <summary>
    /// Revokes an enroll token; it can no longer be used for enrollment.
    /// </summary>
    [HttpPost("{id}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var revoked = await _enrollTokenService.RevokeAsync(id);
        return revoked ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes an enroll token.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _enrollTokenService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
