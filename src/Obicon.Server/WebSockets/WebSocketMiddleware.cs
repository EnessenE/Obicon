using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Obicon.Server.Configuration;
using Obicon.Server.Models;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using Obicon.Shared;
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
    private readonly IServerSettingsService _settingsService;
    private readonly ILogger<WebSocketMiddleware> _logger;

    public WebSocketMiddleware(
        RequestDelegate next,
        NodeConnectionManager connectionManager,
        INodeService nodeService,
        ITestService testService,
        ITestQueueService queueService,
        IServerSettingsService settingsService,
        ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _nodeService = nodeService;
        _testService = testService;
        _queueService = queueService;
        _settingsService = settingsService;
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
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();

            var added = _connectionManager.TryAddConnection(node.Id.ToString(), node.Name, webSocket);
            if (!added)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Node already connected", CancellationToken.None);
                return;
            }

            _logger.LogInformation("Node {NodeId} connected via WebSocket from {RemoteIp}", node.Id, remoteIp ?? "unknown");

            try
            {
                // Announce the server version first so the node can log it and check compatibility
                await SendServerHelloAsync(webSocket);
                await HandleWebSocketConnection(node.Id.ToString(), remoteIp, webSocket);
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

    /// <summary>
    /// Sends the server hello: version plus the observability policy, so nodes know
    /// whether they may ship logs and the server's default for local node logging.
    /// </summary>
    private async Task SendServerHelloAsync(WebSocket webSocket)
    {
        var hello = new WebSocketMessage
        {
            Type = MessageType.ServerHello,
            Data = new ServerHelloMessage
            {
                ServerVersion = ServerInfo.Version,
                LogShippingEnabled = await _settingsService.GetAsync<bool>("NodeLogShippingEnabled"),
                NodeLocalLoggingEnabled = await _settingsService.GetAsync<bool>("NodeLocalLoggingEnabled")
            }
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(hello));
        await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task HandleWebSocketConnection(string nodeId, string? remoteIp, WebSocket webSocket)
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
                    var keepConnection = await ProcessMessage(nodeId, remoteIp, webSocket, wsMessage);
                    if (!keepConnection)
                    {
                        return;
                    }
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

    /// <summary>
    /// Processes one message from a node. Returns false when the connection must be closed,
    /// e.g. because the node version is unsupported.
    /// </summary>
    private async Task<bool> ProcessMessage(string nodeId, string? remoteIp, WebSocket webSocket, WebSocketMessage message)
    {
        _logger.LogDebug("Processing message type: {MessageType} from {NodeId}", message.Type, nodeId);

        switch (message.Type)
        {
            case MessageType.NodeRegistration:
                return await HandleNodeRegistration(nodeId, remoteIp, message);
            case MessageType.NodeHeartbeat:
                await HandleNodeHeartbeat(nodeId);
                return true;
            case MessageType.TestResult:
                await HandleTestResult(nodeId, message);
                return true;
            case MessageType.TestStatusUpdate:
                await HandleTestStatusUpdate(nodeId, message);
                return true;
            case MessageType.ErrorReport:
                HandleErrorReport(nodeId, message);
                return true;
            case MessageType.NodeLog:
                await HandleNodeLogAsync(nodeId, message);
                return true;
            default:
                _logger.LogWarning("Unknown message type: {MessageType}", message.Type);
                return true;
        }
    }

    /// <summary>
    /// Handles a log entry shipped by a node. Entries are dropped while
    /// NodeLogShippingEnabled is off; while ShipNodeLogsToConsole is on they are
    /// written to the server's own console and log, tagged with the node's identity.
    /// </summary>
    private async Task HandleNodeLogAsync(string nodeId, WebSocketMessage message)
    {
        if (!await _settingsService.GetAsync<bool>("NodeLogShippingEnabled"))
        {
            _logger.LogDebug("Dropped log entry from node {NodeId}: log shipping is disabled", nodeId);
            return;
        }

        var entry = (message.Data as JsonElement?)?.Deserialize<NodeLogMessage>();
        if (entry == null || string.IsNullOrWhiteSpace(entry.Message))
        {
            _logger.LogWarning("Received unusable log entry from node {NodeId}", nodeId);
            return;
        }

        if (!await _settingsService.GetAsync<bool>("ShipNodeLogsToConsole"))
        {
            _logger.LogDebug("Received log entry from node {NodeId}; ShipNodeLogsToConsole is off, not forwarding", nodeId);
            return;
        }

        var timestamp = entry.Timestamp == default ? DateTime.UtcNow : entry.Timestamp;
        var text = $"[node {nodeId}] {entry.Message}{(string.IsNullOrWhiteSpace(entry.Exception) ? string.Empty : $" | {entry.Exception}")}";
        switch (entry.Level)
        {
            case "Error":
                _logger.LogError("[node log {Timestamp:O}] {Text}", timestamp, text);
                break;
            case "Warning":
                _logger.LogWarning("[node log {Timestamp:O}] {Text}", timestamp, text);
                break;
            case "Debug":
                _logger.LogDebug("[node log {Timestamp:O}] {Text}", timestamp, text);
                break;
            default:
                _logger.LogInformation("[node log {Timestamp:O}] {Text}", timestamp, text);
                break;
        }
    }

    private async Task<bool> HandleNodeRegistration(string nodeId, string? remoteIp, WebSocketMessage message)
    {
        var registration = (message.Data as JsonElement?)?.Deserialize<NodeRegistrationMessage>();
        var nodeVersion = string.IsNullOrWhiteSpace(registration?.NodeVersion) ? null : registration!.NodeVersion;

        // Version gate: nodes outside the server's supported range (same major.minor)
        // are disconnected, unless the AllowUnsupportedNodeVersions setting is enabled
        if (nodeVersion != null && !ObiconVersions.IsSupported(ServerInfo.Version, nodeVersion))
        {
            if (await _settingsService.GetAsync<bool>("AllowUnsupportedNodeVersions"))
            {
                _logger.LogWarning("Node {NodeId} runs version {NodeVersion}, which is outside the supported range (server {ServerVersion}); AllowUnsupportedNodeVersions is enabled, accepting anyway",
                    nodeId, nodeVersion, ServerInfo.Version);
            }
            else
            {
                _logger.LogWarning("Disconnecting node {NodeId}: version {NodeVersion} is outside the supported range (server {ServerVersion}, same major.minor required)",
                    nodeId, nodeVersion, ServerInfo.Version);
                Metrics.ServerMetrics.Action("node_rejected_version");
                await CloseUnsupportedNodeAsync(nodeId);
                return false;
            }
        }

        _logger.LogInformation("Node {NodeId} registered (version {NodeVersion})", nodeId, nodeVersion ?? "unknown");
        _connectionManager.UpdateLastSeen(nodeId);
        await _nodeService.UpdateNodeLastSeenAsync(Guid.Parse(nodeId));
        await _nodeService.UpdateNodeConnectionInfoAsync(Guid.Parse(nodeId), nodeVersion, remoteIp, CollectReportedSettings(registration));
        return true;
    }

    /// <summary>
    /// Flattens the settings a node reported in its registration into a dictionary.
    /// Older nodes may not report any, yielding an empty dictionary.
    /// </summary>
    private static Dictionary<string, string> CollectReportedSettings(NodeRegistrationMessage? registration)
    {
        var settings = new Dictionary<string, string>();
        if (registration == null)
        {
            return settings;
        }

        if (registration.MaxConcurrentTests is { } maxConcurrentTests)
        {
            settings["MaxConcurrentTests"] = maxConcurrentTests.ToString();
        }
        if (registration.HeartbeatIntervalSeconds is { } heartbeatInterval)
        {
            settings["HeartbeatIntervalSeconds"] = heartbeatInterval.ToString();
        }
        if (registration.DefaultTestTimeoutSeconds is { } defaultTimeout)
        {
            settings["DefaultTestTimeoutSeconds"] = defaultTimeout.ToString();
        }
        if (registration.MaxTestTimeoutSeconds is { } maxTimeout)
        {
            settings["MaxTestTimeoutSeconds"] = maxTimeout.ToString();
        }
        if (registration.ReconnectDelaySeconds is { } reconnectDelay)
        {
            settings["ReconnectDelaySeconds"] = reconnectDelay.ToString();
        }
        return settings;
    }

    /// <summary>
    /// Closes the WebSocket of a node whose version is not supported, so it reconnects
    /// only after an upgrade or after the compatibility flag is enabled.
    /// </summary>
    private async Task CloseUnsupportedNodeAsync(string nodeId)
    {
        var connection = _connectionManager.GetConnection(nodeId);
        if (connection == null)
        {
            return;
        }

        try
        {
            await connection.Socket.CloseAsync(
                WebSocketCloseStatus.PolicyViolation,
                $"Node version not supported by server {ServerInfo.Version}",
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Close handshake with unsupported node {NodeId} aborted", nodeId);
        }
    }

    private static Task CloseConnectionAsync(WebSocket? webSocket, string nodeId)
    {
        // Reserved for callers that need a close without a live connection manager entry
        return Task.CompletedTask;
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
