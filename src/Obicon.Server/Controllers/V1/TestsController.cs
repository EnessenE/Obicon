using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;

namespace Obicon.Server.Controllers.V1;

/// <summary>
/// Test management: creating, editing, and running saved tests.
/// </summary>
[ApiController]
[Route("v1/tests")]
[Produces("application/json")]
public class TestsController : ControllerBase
{
    private readonly ITestService _testService;

    public TestsController(ITestService testService)
    {
        _testService = testService;
    }

    /// <summary>
    /// Returns one page of tests, oldest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<TestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTests([FromQuery] PageParameters page)
    {
        var result = await _testService.GetTestsAsync(page);
        return Ok(new PageResponse<TestResponse>
        {
            Items = result.Items,
            Total = result.Total,
            Limit = result.Limit,
            Offset = result.Offset
        });
    }

    /// <summary>
    /// Returns every test type with whether the server currently offers it.
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(List<TestTypeInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTestTypes()
    {
        return Ok(await _testService.GetTestTypesAsync());
    }

    /// <summary>
    /// Creates a test.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTest([FromBody] TestRequest request)
    {
        var test = await _testService.CreateTestAsync(request);
        return CreatedAtAction(nameof(GetTest), new { id = test.Id }, test);
    }

    /// <summary>
    /// Returns a single test.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTest(Guid id)
    {
        var test = await _testService.GetTestAsync(id);
        return test == null ? NotFound() : Ok(test);
    }

    /// <summary>
    /// Returns the active tests that directly target the given node. Pool targeting is
    /// not considered.
    /// </summary>
    [HttpGet("node/{nodeId}")]
    [ProducesResponseType(typeof(List<TestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTestsForNode(Guid nodeId)
    {
        return Ok(await _testService.GetTestsForNodeAsync(nodeId));
    }

    /// <summary>
    /// Replaces the test with the given field values, like create.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTest(Guid id, [FromBody] TestRequest request)
    {
        var test = await _testService.UpdateTestAsync(id, request);
        return test == null ? NotFound() : Ok(test);
    }

    /// <summary>
    /// Partially updates a test; currently the active state.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(TestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchTest(Guid id, [FromBody] PatchTestRequest request)
    {
        var test = await _testService.SetTestActiveAsync(id, request.IsActive!.Value);
        return test == null ? NotFound() : Ok(test);
    }

    /// <summary>
    /// Triggers an immediate run of the test and returns the created jobs -
    /// one per targeted node and IP family.
    /// </summary>
    [HttpPost("{id}/runs")]
    [ProducesResponseType(typeof(List<TestJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunTest(Guid id)
    {
        var jobs = await _testService.RunTestAsync(id);
        return Ok(jobs.Select(TestJobResponse.From));
    }

    /// <summary>
    /// Deletes a test.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTest(Guid id)
    {
        return await _testService.DeleteTestAsync(id) ? NoContent() : NotFound();
    }
}
