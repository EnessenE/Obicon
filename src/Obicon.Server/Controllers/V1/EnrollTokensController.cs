using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

[ApiController]
[Route("v1/enroll-tokens")]
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
    public async Task<IActionResult> Create([FromBody] CreateEnrollTokenRequest request)
    {
        var token = await _enrollTokenService.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = token.Id }, token);
    }

    /// <summary>
    /// Returns all enroll tokens with their state. Plain tokens are never included.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tokens = await _enrollTokenService.GetAllAsync();
        return Ok(tokens);
    }

    /// <summary>
    /// Revokes an enroll token; it can no longer be used for enrollment.
    /// </summary>
    [HttpPost("{id}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var revoked = await _enrollTokenService.RevokeAsync(id);
        return revoked ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes an enroll token.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _enrollTokenService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
