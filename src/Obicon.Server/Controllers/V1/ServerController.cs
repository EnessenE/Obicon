using Microsoft.AspNetCore.Mvc;
using Obicon.Server.Configuration;
using Obicon.Server.Models.Responses;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Controllers.V1;

[ApiController]
[Route("v1/server")]
public class ServerController : ControllerBase
{
    private readonly INodeService _nodeService;
    private readonly ITestService _testService;
    private readonly ITestQueueService _queueService;
    private readonly NodeConnectionManager _connectionManager;

    public ServerController(
        INodeService nodeService,
        ITestService testService,
        ITestQueueService queueService,
        NodeConnectionManager connectionManager)
    {
        _nodeService = nodeService;
        _testService = testService;
        _queueService = queueService;
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// Returns aggregated statistics about tests, nodes, and the job queue.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ServerStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var nodes = await _nodeService.GetAllNodesAsync();
        var tests = await _testService.GetAllTestsAsync();
        var statusCounts = await _queueService.GetStatusCountsAsync();

        int Count(TestJobStatus status) => statusCounts.TryGetValue(status, out var count) ? count : 0;

        return Ok(new ServerStatsResponse
        {
            Version = ServerInfo.Version,
            TotalNodes = nodes.Count(),
            ConnectedNodes = _connectionManager.GetAllConnections().Count(),
            TotalTests = tests.Count(),
            ActiveTests = tests.Count(t => t.IsActive),
            QueuedJobs = Count(TestJobStatus.Queued),
            RunningJobs = Count(TestJobStatus.Assigned) + Count(TestJobStatus.Running),
            CompletedJobs = Count(TestJobStatus.Completed),
            FailedJobs = Count(TestJobStatus.Failed),
            TimedOutJobs = Count(TestJobStatus.Timeout),
            NoRunJobs = Count(TestJobStatus.NoRun),
            Uptime = DateTimeOffset.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            Timestamp = DateTime.UtcNow
        });
    }
}
