using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Server settings: the knobs the settings UI edits. Values forced by appsettings
/// or environment variables are read-only.
/// </summary>
[ApiController]
[Route("v1/settings")]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly IServerSettingsService _settingsService;

    public SettingsController(IServerSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Returns all server settings with their effective value, description, and whether
    /// configuration forces them (forced settings are read-only).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ServerSettingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var settings = await _settingsService.GetAllAsync();
        return Ok(settings);
    }

    /// <summary>
    /// Changes a server setting. Returns 409 when the setting is forced by appsettings
    /// or an environment variable, and 400 for unknown settings or invalid values.
    /// </summary>
    [HttpPut("{key}")]
    [ProducesResponseType(typeof(ServerSettingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Set(string key, [FromBody] UpdateSettingValueRequest request)
    {
        var setting = await _settingsService.SetAsync(key, request.Value);
        return Ok(setting);
    }
}
