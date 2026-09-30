using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

[ApiController]
[Route("v1/[controller]")]
public class QueueController : ControllerBase
{
    private readonly ITestQueueService _queueService;

    public QueueController(ITestQueueService queueService)
    {
        _queueService = queueService;
    }

    /// <summary>
    /// Returns all test jobs in the queue, newest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TestJobResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllJobs()
    {
        var jobs = await _queueService.GetAllJobsAsync();
        var responses = jobs.Select(TestJobResponse.From);
        return Ok(responses);
    }

    /// <summary>
    /// Returns a single test job by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TestJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await _queueService.GetJobAsync(id);
        if (job == null)
        {
            return NotFound();
        }
        return Ok(TestJobResponse.From(job));
    }
}
