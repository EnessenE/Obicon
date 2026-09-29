using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Enums;
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

    [HttpPost]
    public async Task<IActionResult> CreateTest([FromBody] CreateTestRequest request)
    {
        var test = await _testService.CreateTestAsync(request);
        return CreatedAtAction(nameof(GetTest), new { id = test.Id }, test);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTests()
    {
        var tests = await _testService.GetAllTestsAsync();
        return Ok(tests);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTest(Guid id)
    {
        var test = await _testService.GetTestAsync(id);
        if (test == null)
        {
            return NotFound();
        }
        return Ok(test);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTest(
        Guid id,
        [FromBody] UpdateTestRequest request)
    {
        var test = await _testService.UpdateTestAsync(
            id,
            request.Type,
            request.Target,
            request.NodeIds,
            request.Frequency,
            request.IsActive);
        
        if (test == null)
        {
            return NotFound();
        }
        return Ok(test);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTest(Guid id)
    {
        var deleted = await _testService.DeleteTestAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{id}/run")]
    public async Task<IActionResult> TriggerTestRun(Guid id)
    {
        var triggered = await _testService.TriggerTestRunAsync(id);
        if (!triggered)
        {
            return NotFound();
        }
        return Ok(new { Message = "Test run triggered" });
    }

    /// <summary>
    /// Runs a single test immediately on the given node without creating a test.
    /// The returned job can be polled at GET /v1/queue/{jobId}.
    /// </summary>
    [HttpPost("run-once")]
    public async Task<IActionResult> RunTestOnce([FromBody] RunTestOnceRequest request)
    {
        var job = await _testService.RunOnceAsync(request);
        if (job == null)
        {
            return BadRequest(new { Message = "Node not found or not connected" });
        }
        return Ok(TestJobResponse.From(job));
    }

    [HttpGet("node/{nodeId}")]
    public async Task<IActionResult> GetTestsForNode(Guid nodeId)
    {
        var tests = await _testService.GetTestsForNodeAsync(nodeId);
        return Ok(tests);
    }
}
