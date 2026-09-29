using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Server.BackgroundServices;

/// <summary>
/// Dispatches queued test jobs to connected nodes and reaps jobs whose node never reported back.
/// </summary>
public class TestQueueProcessor : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly ITestQueueService _queueService;
    private readonly NodeConnectionManager _connectionManager;
    private readonly ILogger<TestQueueProcessor> _logger;

    public TestQueueProcessor(
        ITestQueueService queueService,
        NodeConnectionManager connectionManager,
        ILogger<TestQueueProcessor> logger)
    {
        _queueService = queueService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TestQueueProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingJobsAsync(stoppingToken);
                await ReapStaleJobsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TestQueueProcessor");
            }

            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("TestQueueProcessor stopped");
    }

    private async Task DispatchPendingJobsAsync(CancellationToken stoppingToken)
    {
        var jobs = await _queueService.GetPendingJobsAsync();

        foreach (var job in jobs)
        {
            var connection = _connectionManager.GetConnection(job.NodeId.ToString());
            if (connection == null)
            {
                continue;
            }

            var assignment = new WebSocketMessage
            {
                Type = MessageType.TestAssignment,
                Data = new TestAssignmentMessage
                {
                    JobId = job.Id.ToString(),
                    TestId = job.TestId.ToString(),
                    TestType = job.TestType,
                    Target = job.Target,
                    TimeoutSeconds = job.TimeoutSeconds,
                    ExpectedStatusCodes = job.ExpectedStatusCodes,
                    CheckCertificateExpiryDays = job.CheckCertificateExpiryDays,
                    ExpectedDnsResult = job.ExpectedDnsResult,
                    IpVersion = job.IpVersion
                }
            };

            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(assignment, JsonOptions));
                await connection.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, stoppingToken);
                await _queueService.MarkJobAssignedAsync(job.Id);
                _logger.LogInformation("Assigned job {JobId} to node {NodeId} ({TestType} {Target})", job.Id, job.NodeId, job.TestType, job.Target);
                Metrics.ServerMetrics.Action("job_dispatched");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to assign job {JobId} to node {NodeId}", job.Id, job.NodeId);
            }
        }
    }

    private async Task ReapStaleJobsAsync()
    {
        var activeJobs = await _queueService.GetActiveJobsAsync();

        foreach (var job in activeJobs)
        {
            var reference = job.Status == TestJobStatus.Running ? job.StartedAt : job.CreatedAt;
            if (reference == null)
            {
                continue;
            }

            var limit = TimeSpan.FromSeconds(job.TimeoutSeconds + 15);
            if (DateTime.UtcNow - reference > limit)
            {
                await _queueService.UpdateJobStatusAsync(
                    job.Id,
                    TestJobStatus.Timeout,
                    errorMessage: $"No result from node within {job.TimeoutSeconds + 15}s");
                _logger.LogWarning("Reaped job {JobId}: no result from node {NodeId}", job.Id, job.NodeId);
            }
        }
    }
}
