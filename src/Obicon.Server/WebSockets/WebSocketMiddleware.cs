using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Obicon.Server.Configuration;
using Obicon.Server.Metrics;
using Obicon.Server.Models;
using Obicon.Server.Services;
using Obicon.Shared;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Server.WebSockets;

public partial class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly NodeConnectionManager _connectionManager;
    private readonly INodeService _nodeService;
    private readonly ITestService _testService;
    private readonly ITestQueueService _queueService;
    private readonly IServerSettingsService _settingsService;
    private readonly ITestMetricsEmitter _testMetricsEmitter;
    private readonly ILogger<WebSocketMiddleware> _logger;

    public WebSocketMiddleware(
        RequestDelegate next,
        NodeConnectionManager connectionManager,
        INodeService nodeService,
        ITestService testService,
        ITestQueueService queueService,
        IServerSettingsService settingsService,
        ITestMetricsEmitter testMetricsEmitter,
        ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _nodeService = nodeService;
        _testService = testService;
        _queueService = queueService;
        _settingsService = settingsService;
        _testMetricsEmitter = testMetricsEmitter;
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

            LogNodeConnected(node.Id, remoteIp ?? "unknown");

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
                LogNodeDisconnected(node.Id);
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
                NodeLocalLoggingEnabled = await _settingsService.GetAsync<bool>("NodeLocalLoggingEnabled"),
                ExternalIpResolvingEnabled = await _settingsService.GetAsync<bool>("NodeExternalIpResolvingEnabled")
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
            LogMessageReceived(nodeId, message);

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
                LogMessageProcessingError(ex, nodeId);
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
            LogCloseHandshakeAborted(ex, nodeId);
        }
    }

    /// <summary>
    /// Processes one message from a node. Returns false when the connection must be closed,
    /// e.g. because the node version is unsupported.
    /// </summary>
    private async Task<bool> ProcessMessage(string nodeId, string? remoteIp, WebSocket webSocket, WebSocketMessage message)
    {
        LogProcessingMessage(message.Type, nodeId);

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
            case MessageType.NodeInfoUpdate:
                await HandleNodeInfoUpdate(nodeId, message);
                return true;
            default:
                LogUnknownMessageType(message.Type);
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
            LogDroppedNodeLog(nodeId);
            return;
        }

        var entry = (message.Data as JsonElement?)?.Deserialize<NodeLogMessage>();
        if (entry == null || string.IsNullOrWhiteSpace(entry.Message))
        {
            LogUnusableNodeLog(nodeId);
            return;
        }

        // Every received entry is counted in OpenTelemetry, whether or not it is
        // also forwarded to the console
        var node = await _nodeService.GetNodeAsync(Guid.Parse(nodeId));
        var sourceContext = entry.Properties is { } props && props.TryGetValue("SourceContext", out var sc)
            ? sc.ToString().Trim('"')
            : "unknown";
        Metrics.ServerMetrics.NodeLog(
            string.IsNullOrWhiteSpace(entry.Level) ? "unknown" : entry.Level,
            sourceContext,
            nodeId,
            node?.Name ?? "unknown");

        if (!await _settingsService.GetAsync<bool>("ShipNodeLogsToConsole"))
        {
            LogNodeLogNotForwarded(nodeId);
            return;
        }

        var timestamp = entry.Timestamp == default ? DateTime.UtcNow : entry.Timestamp;
        var origin = string.IsNullOrWhiteSpace(entry.NodeName)
            ? nodeId
            : $"{entry.NodeName} ({nodeId})";
        if (!string.IsNullOrWhiteSpace(entry.NodeVersion))
        {
            origin += $" v{entry.NodeVersion}";
        }

        // Compact rendering of the entry's structured properties, e.g. SourceContext and JobId
        var properties = entry.Properties is { Count: > 0 }
            ? " {" + string.Join(", ", entry.Properties.Select(kv => $"{kv.Key}={Truncate(kv.Value, 200)}")) + "}"
            : string.Empty;

        var text = $"[node {origin}] {entry.Message}{properties}" +
                   (string.IsNullOrWhiteSpace(entry.Exception) ? string.Empty : $" | {entry.Exception}");

        switch (entry.Level)
        {
            case "Error":
                LogNodeLogError(timestamp, text);
                break;
            case "Warning":
                LogNodeLogWarning(timestamp, text);
                break;
            case "Debug":
                LogNodeLogDebug(timestamp, text);
                break;
            default:
                LogNodeLogInformation(timestamp, text);
                break;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength] + "…";
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
                LogUnsupportedNodeAccepted(nodeId, nodeVersion, ServerInfo.Version);
            }
            else
            {
                LogUnsupportedNodeDisconnected(nodeId, nodeVersion, ServerInfo.Version);
                Metrics.ServerMetrics.Action("node_rejected_version");
                await CloseUnsupportedNodeAsync(nodeId);
                return false;
            }
        }

        LogNodeRegistered(nodeId, nodeVersion ?? "unknown");
        _connectionManager.UpdateLastSeen(nodeId);
        await _nodeService.UpdateNodeLastSeenAsync(Guid.Parse(nodeId));
        await _nodeService.UpdateNodeConnectionInfoAsync(Guid.Parse(nodeId), nodeVersion, remoteIp, CollectReportedSettings(registration));
        await _nodeService.UpdateNodeReportedAddressesAsync(Guid.Parse(nodeId),
            registration?.InternalIpv4, registration?.InternalIpv6, registration?.ExternalIpv4, registration?.ExternalIpv6);
        return true;
    }

    /// <summary>
    /// Handles a node's address refresh: it re-resolves its internal and external IP every
    /// so often and reports changes here, without waiting for a reconnect.
    /// </summary>
    private async Task HandleNodeInfoUpdate(string nodeId, WebSocketMessage message)
    {
        var update = (message.Data as JsonElement?)?.Deserialize<NodeInfoUpdateMessage>();
        if (update == null)
        {
            LogUnusableAddressUpdate(nodeId);
            return;
        }

        await _nodeService.UpdateNodeReportedAddressesAsync(Guid.Parse(nodeId),
            update.InternalIpv4, update.InternalIpv6, update.ExternalIpv4, update.ExternalIpv6);
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
            settings["MaxConcurrentTests"] = maxConcurrentTests.ToString(CultureInfo.InvariantCulture);
        }
        if (registration.HeartbeatIntervalSeconds is { } heartbeatInterval)
        {
            settings["HeartbeatIntervalSeconds"] = heartbeatInterval.ToString(CultureInfo.InvariantCulture);
        }
        if (registration.DefaultTestTimeoutSeconds is { } defaultTimeout)
        {
            settings["DefaultTestTimeoutSeconds"] = defaultTimeout.ToString(CultureInfo.InvariantCulture);
        }
        if (registration.MaxTestTimeoutSeconds is { } maxTimeout)
        {
            settings["MaxTestTimeoutSeconds"] = maxTimeout.ToString(CultureInfo.InvariantCulture);
        }
        if (registration.ReconnectDelaySeconds is { } reconnectDelay)
        {
            settings["ReconnectDelaySeconds"] = reconnectDelay.ToString(CultureInfo.InvariantCulture);
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
            LogCloseUnsupportedNodeAborted(ex, nodeId);
        }
    }

    private static Task CloseConnectionAsync(WebSocket? webSocket, string nodeId)
    {
        // Reserved for callers that need a close without a live connection manager entry
        return Task.CompletedTask;
    }

    private async Task HandleNodeHeartbeat(string nodeId)
    {
        LogHeartbeat(nodeId);
        _connectionManager.UpdateLastSeen(nodeId);
        await _nodeService.UpdateNodeLastSeenAsync(Guid.Parse(nodeId));
    }

    private async Task HandleTestResult(string nodeId, WebSocketMessage message)
    {
        var result = (message.Data as JsonElement?)?.Deserialize<TestResultMessage>();
        if (result == null || !Guid.TryParse(result.JobId, out var jobId))
        {
            LogTestResultWithoutJobId(nodeId);
            return;
        }

        var status = result.Success ? TestJobStatus.Completed : TestJobStatus.Failed;
        var testResult = new TestResult
        {
            Success = result.Success,
            DurationMs = result.DurationMs,
            Output = result.Output,
            Details = result.Details
        };

        await _queueService.UpdateJobStatusAsync(jobId, status, testResult);

        var job = await _queueService.GetJobAsync(jobId);
        var test = job != null && job.TestId != Guid.Empty ? await _testService.GetTestAsync(job.TestId) : null;
        var node = await _nodeService.GetNodeAsync(Guid.Parse(nodeId));

        await _testMetricsEmitter.EmitAsync(job, test, node, status, result.DurationMs);

        LogJobFinished(jobId, nodeId, result.Success, result.DurationMs);
    }

    private async Task HandleTestStatusUpdate(string nodeId, WebSocketMessage message)
    {
        var update = (message.Data as JsonElement?)?.Deserialize<TestStatusUpdateMessage>();
        if (update == null || !Guid.TryParse(update.JobId, out var jobId))
        {
            LogStatusUpdateWithoutJobId(nodeId);
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
            LogJobAcknowledged(nodeId, jobId);
        }
    }

    private void HandleErrorReport(string nodeId, WebSocketMessage message)
    {
        LogErrorReport(nodeId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Node {NodeId} connected via WebSocket from {RemoteIp}")]
    private partial void LogNodeConnected(Guid nodeId, string remoteIp);

    [LoggerMessage(Level = LogLevel.Information, Message = "Node {NodeId} disconnected")]
    private partial void LogNodeDisconnected(Guid nodeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received from {NodeId}: {Message}")]
    private partial void LogMessageReceived(string nodeId, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing message from {NodeId}")]
    private partial void LogMessageProcessingError(Exception exception, string nodeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Close handshake with node {NodeId} aborted")]
    private partial void LogCloseHandshakeAborted(Exception exception, string nodeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing message type: {MessageType} from {NodeId}")]
    private partial void LogProcessingMessage(MessageType messageType, string nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown message type: {MessageType}")]
    private partial void LogUnknownMessageType(MessageType messageType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped log entry from node {NodeId}: log shipping is disabled")]
    private partial void LogDroppedNodeLog(string nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received unusable log entry from node {NodeId}")]
    private partial void LogUnusableNodeLog(string nodeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received log entry from node {NodeId}; ShipNodeLogsToConsole is off, not forwarding")]
    private partial void LogNodeLogNotForwarded(string nodeId);

    [LoggerMessage(Level = LogLevel.Error, Message = "[node log {Timestamp:HH:mm:ss}] {Text}")]
    private partial void LogNodeLogError(DateTime timestamp, string text);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[node log {Timestamp:HH:mm:ss}] {Text}")]
    private partial void LogNodeLogWarning(DateTime timestamp, string text);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[node log {Timestamp:HH:mm:ss}] {Text}")]
    private partial void LogNodeLogDebug(DateTime timestamp, string text);

    [LoggerMessage(Level = LogLevel.Information, Message = "[node log {Timestamp:HH:mm:ss}] {Text}")]
    private partial void LogNodeLogInformation(DateTime timestamp, string text);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Node {NodeId} runs version {NodeVersion}, which is outside the supported range (server {ServerVersion}); AllowUnsupportedNodeVersions is enabled, accepting anyway")]
    private partial void LogUnsupportedNodeAccepted(string nodeId, string? nodeVersion, string serverVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disconnecting node {NodeId}: version {NodeVersion} is outside the supported range (server {ServerVersion}, same major.minor required)")]
    private partial void LogUnsupportedNodeDisconnected(string nodeId, string? nodeVersion, string serverVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Node {NodeId} registered (version {NodeVersion})")]
    private partial void LogNodeRegistered(string nodeId, string nodeVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received unusable address update from node {NodeId}")]
    private partial void LogUnusableAddressUpdate(string nodeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Close handshake with unsupported node {NodeId} aborted")]
    private partial void LogCloseUnsupportedNodeAborted(Exception exception, string nodeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat from {NodeId}")]
    private partial void LogHeartbeat(string nodeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received test result from {NodeId} without a valid job ID")]
    private partial void LogTestResultWithoutJobId(string nodeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} finished on node {NodeId}: success={Success} duration={DurationMs}ms")]
    private partial void LogJobFinished(Guid jobId, string nodeId, bool success, long durationMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received status update from {NodeId} without a valid job ID")]
    private partial void LogStatusUpdateWithoutJobId(string nodeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Node {NodeId} acknowledged job {JobId}")]
    private partial void LogJobAcknowledged(string nodeId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error report from {NodeId}")]
    private partial void LogErrorReport(string nodeId);
}
