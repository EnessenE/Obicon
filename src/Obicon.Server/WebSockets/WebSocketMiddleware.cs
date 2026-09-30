using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Obicon.Server.Models;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Server.WebSockets;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly NodeConnectionManager _connectionManager;
    private readonly INodeService _nodeService;
    private readonly ITestService _testService;
    private readonly ITestQueueService _queueService;
    private readonly ILogger<WebSocketMiddleware> _logger;

    public WebSocketMiddleware(
        RequestDelegate next,
        NodeConnectionManager connectionManager,
        INodeService nodeService,
        ITestService testService,
        ITestQueueService queueService,
        ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _nodeService = nodeService;
        _testService = testService;
        _queueService = queueService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/ws/nodes" &&
            context.WebSockets.IsWebSocketRequest)
        {
            var token = context.Request.Query["token"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing token");
                return;
            }

            var node = await _nodeService.GetNodeByTokenAsync(token);
            if (node == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token");
                return;
            }

            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString();

            var added = _connectionManager.TryAddConnection(node.Id.ToString(), node.Name, webSocket);
            if (!added)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Node already connected", CancellationToken.None);
                return;
            }

            _logger.LogInformation("Node {NodeId} connected via WebSocket", node.Id);

            try
            {
                await HandleWebSocketConnection(node.Id.ToString(), webSocket);
            }
            finally
            {
                // Always drop the connection, also when the socket aborted mid-close-handshake
                _connectionManager.TryRemoveConnection(node.Id.ToString());
                _logger.LogInformation("Node {NodeId} disconnected", node.Id);
            }

            return;
        }

        await _next(context);
    }

    private async Task HandleWebSocketConnection(string nodeId, WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            _logger.LogDebug("Received from {NodeId}: {Message}", nodeId, message);

            try
            {
                var wsMessage = JsonSerializer.Deserialize<WebSocketMessage>(message);
                if (wsMessage != null)
                {
                    await ProcessMessage(nodeId, webSocket, wsMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {NodeId}", nodeId);
            }

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        try
        {
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The node may have dropped the socket before completing the close handshake
            _logger.LogDebug(ex, "Close handshake with node {NodeId} aborted", nodeId);
        }
    }

    private async Task ProcessMessage(string nodeId, WebSocket webSocket, WebSocketMessage message)
    {
        _logger.LogDebug("Processing message type: {MessageType} from {NodeId}", message.Type, nodeId);

        switch (message.Type)
        {
            case MessageType.NodeRegistration:
                await HandleNodeRegistration(nodeId);
                break;
            case MessageType.NodeHeartbeat:
                await HandleNodeHeartbeat(nodeId);
                break;
            case MessageType.TestResult:
                await HandleTestResult(nodeId, message);
                break;
            case MessageType.TestStatusUpdate:
                await HandleTestStatusUpdate(nodeId, message);
                break;
            case MessageType.ErrorReport:
                HandleErrorReport(nodeId, message);
                break;
            default:
                _logger.LogWarning("Unknown message type: {MessageType}", message.Type);
                break;
        }
    }

    private async Task HandleNodeRegistration(string nodeId)
    {
        _logger.LogInformation("Node {NodeId} registered", nodeId);
        _connectionManager.UpdateLastSeen(nodeId);
        await _nodeService.UpdateNodeLastSeenAsync(Guid.Parse(nodeId));
    }

    private async Task HandleNodeHeartbeat(string nodeId)
    {
        _logger.LogDebug("Heartbeat from {NodeId}", nodeId);
        _connectionManager.UpdateLastSeen(nodeId);
        await _nodeService.UpdateNodeLastSeenAsync(Guid.Parse(nodeId));
    }

    private async Task HandleTestResult(string nodeId, WebSocketMessage message)
    {
        var result = (message.Data as JsonElement?)?.Deserialize<TestResultMessage>();
        if (result == null || !Guid.TryParse(result.JobId, out var jobId))
        {
            _logger.LogWarning("Received test result from {NodeId} without a valid job ID", nodeId);
            return;
        }

        var status = result.Success ? TestJobStatus.Completed : TestJobStatus.Failed;
        var testResult = new TestResult
        {
            Success = result.Success,
            DurationMs = result.DurationMs,
            Output = result.Output,
            Metrics = result.Metrics
        };

        await _queueService.UpdateJobStatusAsync(jobId, status, testResult);

        var job = await _queueService.GetJobAsync(jobId);
        var test = job != null && job.TestId != Guid.Empty ? await _testService.GetTestAsync(job.TestId) : null;
        var node = await _nodeService.GetNodeAsync(Guid.Parse(nodeId));

        Metrics.ServerMetrics.TestRun(
            status.ToString(),
            job?.TestType.ToString() ?? "unknown",
            job?.TestId.ToString() ?? "unknown",
            test?.Name ?? "run-once",
            nodeId,
            node?.Name ?? "unknown",
            result.DurationMs);

        _logger.LogInformation("Job {JobId} finished on node {NodeId}: success={Success} duration={DurationMs}ms",
            jobId, nodeId, result.Success, result.DurationMs);
    }

    private async Task HandleTestStatusUpdate(string nodeId, WebSocketMessage message)
    {
        var update = (message.Data as JsonElement?)?.Deserialize<TestStatusUpdateMessage>();
        if (update == null || !Guid.TryParse(update.JobId, out var jobId))
        {
            _logger.LogWarning("Received status update from {NodeId} without a valid job ID", nodeId);
            return;
        }

        // Final statuses are derived from the TestResult message; track the
        // assignment acknowledgment and execution start here
        if (update.Status == TestJobStatus.Running)
        {
            await _queueService.MarkJobStartedAsync(jobId);
        }
        else if (update.Status == TestJobStatus.Assigned)
        {
            await _queueService.MarkJobAcknowledgedAsync(jobId);
            _logger.LogInformation("Node {NodeId} acknowledged job {JobId}", nodeId, jobId);
        }
    }

    private void HandleErrorReport(string nodeId, WebSocketMessage message)
    {
        _logger.LogError("Error report from {NodeId}", nodeId);
    }
}
