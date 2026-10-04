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
/// The NoRun scenario is checked on a fixed 10-second cadence.
/// </summary>
public partial class TestQueueProcessor : BackgroundService
{
    private static readonly TimeSpan NoRunCheckInterval = TimeSpan.FromSeconds(10);

    private DateTime _lastNoRunCheck = DateTime.MinValue;
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly ITestQueueService _queueService;
    private readonly NodeConnectionManager _connectionManager;
    private readonly IServerSettingsService _settingsService;
    private readonly ILogger<TestQueueProcessor> _logger;

    public TestQueueProcessor(
        ITestQueueService queueService,
        NodeConnectionManager connectionManager,
        IServerSettingsService settingsService,
        ILogger<TestQueueProcessor> logger)
    {
        _queueService = queueService;
        _connectionManager = connectionManager;
        _settingsService = settingsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingJobsAsync(stoppingToken);

                // The NoRun scenario is checked on a fixed 10-second cadence
                if (DateTime.UtcNow - _lastNoRunCheck >= NoRunCheckInterval)
                {
                    _lastNoRunCheck = DateTime.UtcNow;
                    await ReapStaleJobsAsync();
                }
            }
            catch (Exception ex)
            {
                LogLoopError(ex);
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

        LogStopped();
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
                    IpVersion = job.IpVersion,
                    ExpectedBodyPattern = job.ExpectedBodyPattern,
                    Headers = job.Headers.ToDictionary(h => h.Name, h => h.Value),
                    ProxyUrl = job.ProxyUrl,
                    CacheBust = job.CacheBust,
                    TracerouteMaxHops = job.TracerouteMaxHops,
                    TracerouteQueriesPerHop = job.TracerouteQueriesPerHop,
                    TracerouteQueryTimeoutMs = job.TracerouteQueryTimeoutMs,
                    TracerouteResolveHostnames = job.TracerouteResolveHostnames,
                    PingCount = job.PingCount,
                    PingTimeoutMs = job.PingTimeoutMs,
                    PingIntervalMs = job.PingIntervalMs,
                    HttpMethod = job.HttpMethod,
                    FollowRedirects = job.FollowRedirects,
                    DnsNameserver = job.DnsNameserver,
                    DnsQueryType = job.DnsQueryType
                }
            };

            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(assignment, JsonOptions));
                await connection.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, stoppingToken);
                await _queueService.MarkJobAssignedAsync(job.Id);
                LogJobAssigned(job.Id, job.NodeId, job.TestType, job.Target);
                Metrics.ServerMetrics.Action("job_dispatched");
            }
            catch (Exception ex)
            {
                LogAssignFailed(ex, job.Id, job.NodeId);
            }
        }
    }

    private async Task ReapStaleJobsAsync()
    {
        var graceFactor = Math.Max(1, await _settingsService.GetAsync<int>("NoRunGraceFactor"));

        // A queued job whose node never came online can never run; mark it NoRun
        // after the full [timeout] + 15s window instead of letting it sit forever
        var queuedJobs = await _queueService.GetPendingJobsAsync();
        foreach (var job in queuedJobs)
        {
            var offlineLimit = TimeSpan.FromSeconds(job.TimeoutSeconds + 15);
            if (_connectionManager.GetConnection(job.NodeId.ToString()) == null &&
                DateTime.UtcNow - job.CreatedAt > offlineLimit)
            {
                await _queueService.UpdateJobStatusAsync(
                    job.Id,
                    TestJobStatus.NoRun,
                    errorMessage: $"Node {job.NodeId} was not connected within {offlineLimit.TotalSeconds:F0}s");
                LogMarkedNoRunOffline(job.Id, job.NodeId);
                Metrics.ServerMetrics.NoRun("node_offline");
            }
        }

        var activeJobs = await _queueService.GetActiveJobsAsync();

        foreach (var job in activeJobs)
        {
            if (job.Status == TestJobStatus.Assigned)
            {
                if (job.AcknowledgedAt == null)
                {
                    // Dispatched but the node never acknowledged it: NoRun after
                    // [test timeout] divided by the grace factor, then drop it from the queue
                    var noRunAfter = TimeSpan.FromSeconds((double)job.TimeoutSeconds / graceFactor);
                    if (DateTime.UtcNow - job.CreatedAt > noRunAfter)
                    {
                        await _queueService.UpdateJobStatusAsync(
                            job.Id,
                            TestJobStatus.NoRun,
                            errorMessage: $"Node {job.NodeId} never acknowledged the job within {noRunAfter.TotalSeconds:F0}s");
                        Metrics.ServerMetrics.NoRun("never_acknowledged");
                        LogMarkedNoRunNotAcknowledged(job.Id, job.NodeId);
                    }
                    continue;
                }

                // Acknowledged but never started (e.g. the node is at max concurrency):
                // give it the full [timeout] + 15s before declaring it never ran
                var startLimit = TimeSpan.FromSeconds(job.TimeoutSeconds + 15);
                if (DateTime.UtcNow - job.AcknowledgedAt > startLimit)
                {
                    await _queueService.UpdateJobStatusAsync(
                        job.Id,
                        TestJobStatus.NoRun,
                        errorMessage: $"Node {job.NodeId} acknowledged the job but never started it within {startLimit.TotalSeconds:F0}s");
                    Metrics.ServerMetrics.NoRun("never_started");
                    LogMarkedNoRunNotStarted(job.Id, job.NodeId);
                }
                continue;
            }

            // Running for longer than [timeout] + 15s without a result: mark Timeout
            if (job.StartedAt == null)
            {
                continue;
            }

            var limit = TimeSpan.FromSeconds(job.TimeoutSeconds + 15);
            if (DateTime.UtcNow - job.StartedAt > limit)
            {
                await _queueService.UpdateJobStatusAsync(
                    job.Id,
                    TestJobStatus.Timeout,
                    errorMessage: $"No result from node within {job.TimeoutSeconds + 15}s");
                LogReapedJob(job.Id, job.NodeId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "TestQueueProcessor started")]
    private partial void LogStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "TestQueueProcessor stopped")]
    private partial void LogStopped();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in TestQueueProcessor")]
    private partial void LogLoopError(System.Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigned job {JobId} to node {NodeId} ({TestType} {Target})")]
    private partial void LogJobAssigned(Guid jobId, Guid nodeId, TestType testType, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to assign job {JobId} to node {NodeId}")]
    private partial void LogAssignFailed(System.Exception exception, Guid jobId, Guid nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} marked NoRun: node {NodeId} never came online")]
    private partial void LogMarkedNoRunOffline(Guid jobId, Guid nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} marked NoRun: node {NodeId} never acknowledged it")]
    private partial void LogMarkedNoRunNotAcknowledged(Guid jobId, Guid nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} marked NoRun: node {NodeId} acknowledged but never started it")]
    private partial void LogMarkedNoRunNotStarted(Guid jobId, Guid nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reaped job {JobId}: no result from node {NodeId}")]
    private partial void LogReapedJob(Guid jobId, Guid nodeId);
}
