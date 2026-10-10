using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Test runs: pages of in-flight and finished jobs with server-side filters and
/// sorting, plus an ad-hoc run-once endpoint that needs no saved test.
/// </summary>
[ApiController]
[Route("v1/test-runs")]
[Produces("application/json")]
public class TestRunsController : ControllerBase
{
    private readonly ITestQueueService _queueService;
    private readonly ITestService _testService;

    public TestRunsController(ITestQueueService queueService, ITestService testService)
    {
        _queueService = queueService;
        _testService = testService;
    }

    /// <summary>
    /// Returns one page of test runs with the total number of runs matching the
    /// filters across all pages. Sort by createdAt, durationMs, or status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<TestJobResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuns([FromQuery] TestRunsQueryParameters parameters)
    {
        var sortBy = parameters.SortBy?.Trim().ToLowerInvariant() switch
        {
            "durationms" or "duration" => TestRunSortBy.DurationMs,
            "status" => TestRunSortBy.Status,
            _ => TestRunSortBy.CreatedAt
        };
        var descending = !string.Equals(parameters.SortOrder?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);

        var page = await _queueService.GetRunsAsync(new TestRunQuery(
            Limit: Math.Clamp(parameters.Limit < 1 ? 50 : parameters.Limit, 1, PageParameters.MaxLimit),
            Offset: Math.Max(0, parameters.Offset),
            Status: parameters.Status,
            NodeId: parameters.NodeId,
            TestId: parameters.TestId,
            Search: string.IsNullOrWhiteSpace(parameters.Search) ? null : parameters.Search.Trim(),
            SortBy: sortBy,
            Descending: descending));

        return Ok(new PageResponse<TestJobResponse>
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRun(Guid id)
    {
        var job = await _queueService.GetJobAsync(id);
        if (job == null)
        {
            return NotFound();
        }
        return Ok(TestJobResponse.From(job));
    }

    /// <summary>
    /// Runs a test immediately without creating a saved test first. Jobs go to the
    /// connected nodes among the selected node and pool IDs; one job is created per node.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(List<TestJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunOnce([FromBody] RunTestOnceRequest request)
    {
        var jobs = await _testService.RunOnceAsync(request);
        return Ok(jobs.Select(TestJobResponse.From).ToList());
    }
}
