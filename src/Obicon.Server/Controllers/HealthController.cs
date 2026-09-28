using Microsoft.AspNetCore.Mvc;

namespace Obicon.Server.Controllers;

[ApiController]
[Route("v1/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}
