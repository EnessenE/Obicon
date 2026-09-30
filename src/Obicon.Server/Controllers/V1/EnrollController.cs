using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Node self-enrollment endpoint. Authenticated by the enroll token in the request body
/// instead of the API Authorization header.
/// </summary>
[ApiController]
[Route("v1/enroll")]
public class EnrollController : ControllerBase
{
    private readonly INodeEnrollmentService _enrollmentService;

    public EnrollController(INodeEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    /// <summary>
    /// Registers or updates a node using an enroll token. Requires the NodeAutoEnrollmentEnabled setting.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EnrollResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Enroll([FromBody] EnrollRequest request)
    {
        try
        {
            var result = await _enrollmentService.EnrollAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Message = "Invalid, revoked, or expired enroll token" });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
