using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

[ApiController]
[Route("v1/[controller]")]
public class TestsController : ControllerBase
{
    private readonly ITestService _testService;

    public TestsController(ITestService testService)
    {
        _testService = testService;
    }

    /// <summary>
    /// Creates a new test. Validation errors are answered with 400.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTest([FromBody] CreateTestRequest request)
    {
        try
        {
            var test = await _testService.CreateTestAsync(request);
            return CreatedAtAction(nameof(GetTest), new { id = test.Id }, test);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Returns all tests.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllTests()
    {
        var tests = await _testService.GetAllTestsAsync();
        return Ok(tests);
    }

    /// <summary>
    /// Returns one test by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTest(Guid id)
    {
        var test = await _testService.GetTestAsync(id);
        if (test == null)
        {
            return NotFound();
        }
        return Ok(test);
    }

    /// <summary>
    /// Updates a test. Validation errors are answered with 400.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTest(
        Guid id,
        [FromBody] UpdateTestRequest request)
    {
        try
        {
            var test = await _testService.UpdateTestAsync(id, request);
            if (test == null)
            {
                return NotFound();
            }
            return Ok(test);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Flips a test between active and inactive without deleting it.
    /// </summary>
    [HttpPost("{id}/toggle")]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleTest(Guid id)
    {
        var test = await _testService.ToggleTestAsync(id);
        if (test == null)
        {
            return NotFound();
        }
        return Ok(test);
    }

    /// <summary>
    /// Deletes a test.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTest(Guid id)
    {
        var deleted = await _testService.DeleteTestAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    /// <summary>
    /// Triggers immediate execution of a test on all its targeted nodes.
    /// </summary>
    [HttpPost("{id}/run")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TriggerTestRun(Guid id)
    {
        var triggered = await _testService.TriggerTestRunAsync(id);
        if (!triggered)
        {
            return NotFound();
        }
        return Ok(new MessageResponse { Message = "Test run triggered" });
    }

    /// <summary>
    /// Runs a single test immediately on each selected connected node without creating a test.
    /// Returns one job per node; poll each at GET /v1/queue/{jobId} until it reaches a final status.
    /// </summary>
    [HttpPost("run-once")]
    [ProducesResponseType(typeof(List<TestJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunTestOnce([FromBody] RunTestOnceRequest request)
    {
        try
        {
            var jobs = await _testService.RunOnceAsync(request);
            return Ok(jobs.Select(TestJobResponse.From));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new MessageResponse { Message = ex.Message });
        }
    }

    /// <summary>
    /// Returns the active tests that directly target the given node, ordered by creation time.
    /// </summary>
    [HttpGet("node/{nodeId}")]
    [ProducesResponseType(typeof(List<Test>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTestsForNode(Guid nodeId)
    {
        var tests = await _testService.GetTestsForNodeAsync(nodeId);
        return Ok(tests);
    }
}
