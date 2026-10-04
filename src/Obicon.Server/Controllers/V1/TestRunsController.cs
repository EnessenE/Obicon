using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Test runs: pages of in-flight and finished jobs with server-side filters.
/// </summary>
[ApiController]
[Route("v1/[controller]")]
public class TestRunsController : ControllerBase
{
    /// <summary>
    /// Largest page the listing serves, so one request can never load the whole window.
    /// </summary>
    public const int MaxLimit = 500;

    private readonly ITestQueueService _queueService;

    public TestRunsController(ITestQueueService queueService)
    {
        _queueService = queueService;
    }

    /// <summary>
    /// Returns one page of test runs, newest first, with the total number of runs
    /// matching the filters across all pages.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TestRunsPageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuns([FromQuery] TestRunsQueryParameters parameters)
    {
        var page = await _queueService.GetRunsAsync(new TestRunQuery(
            Limit: Math.Clamp(parameters.Limit < 1 ? 50 : parameters.Limit, 1, MaxLimit),
            Offset: Math.Max(0, parameters.Offset),
            Status: parameters.Status,
            NodeId: parameters.NodeId,
            TestId: parameters.TestId,
            Search: string.IsNullOrWhiteSpace(parameters.Search) ? null : parameters.Search.Trim()));

        return Ok(new TestRunsPageResponse
        {
            Items = page.Items.Select(TestJobResponse.From).ToList(),
            Total = page.Total,
            Limit = page.Limit,
            Offset = page.Offset
        });
    }

    /// <summary>
    /// Returns a single test run by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TestJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRun(Guid id)
    {
        var job = await _queueService.GetJobAsync(id);
        if (job == null)
        {
            return NotFound();
        }
        return Ok(TestJobResponse.From(job));
    }
}
