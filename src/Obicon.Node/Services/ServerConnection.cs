using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Shared;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services;

/// <summary>
/// Dedicated communication task: maintains the WebSocket connection to the primary server,
/// registers the node, sends heartbeats, dispatches test assignments to the executor,
/// and reconnects with a delay on disconnect.
/// </summary>
public partial class ServerConnection : BackgroundService, IServerConnection
{
    private readonly NodeSettings _settings;
    private readonly ITestExecutor _testExecutor;
    private readonly ILogger<ServerConnection> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly NodeIdentityStore _identityStore;
    private readonly EnrollmentClient _enrollmentClient;
    private readonly NodeLoggingState _loggingState;
    private readonly NodeAddressState _addressState;

    private volatile ClientWebSocket? _socket;

    public ServerConnection(
        IOptions<NodeSettings> settings,
        ITestExecutor testExecutor,
        NodeIdentityStore identityStore,
        EnrollmentClient enrollmentClient,
        NodeLoggingState loggingState,
        NodeAddressState addressState,
        ILogger<ServerConnection> logger)
    {
        _settings = settings.Value;
        _testExecutor = testExecutor;
        _identityStore = identityStore;
        _enrollmentClient = enrollmentClient;
        _loggingState = loggingState;
        _addressState = addressState;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    /// <inheritdoc />
    public async Task SendAsync(WebSocketMessage message)
    {
        var socket = _socket;
        if (socket == null || socket.State != WebSocketState.Open)
        {
            LogDroppingMessage(message.Type);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await _sendLock.WaitAsync();
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogSendFailed(ex, message.Type);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Token) && string.IsNullOrWhiteSpace(_settings.EnrollToken) && string.IsNullOrWhiteSpace(_identityStore.AuthToken))
        {
            LogNoTokenConfigured();
        }

        if (!Uri.TryCreate(_settings.ServerUrl, UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != "ws" && serverUri.Scheme != "wss"))
        {
            LogInvalidServerUrl(_settings.ServerUrl);
            return;
        }

        // TLS is required by default: unencrypted ws:// is refused, except for
        // loopback addresses (local development)
        if (_settings.RequireTls && serverUri.Scheme == "ws" && !IsLoopbackHost(serverUri.Host))
        {
            LogTlsRequired(_settings.ServerUrl);
            return;
        }

        if (_settings.HeartbeatIntervalSeconds < 1 || _settings.MaxConcurrentTests < 1 || _settings.DefaultTestTimeoutSeconds < 1)
        {
            LogClampedSettings();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndRunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException ex) when (IsUnauthorized(ex))
            {
                // The stored token was rejected (e.g. regenerated on the server): re-enroll if possible
                if (string.IsNullOrWhiteSpace(_settings.Token) && !string.IsNullOrWhiteSpace(_settings.EnrollToken))
                {
                    LogTokenRejected();
                    _identityStore.Reset();
                }
            }
            catch (Exception ex)
            {
                LogConnectionLost(ex);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                LogReconnecting(_settings.ReconnectDelaySeconds);
                Metrics.NodeMetrics.Reconnect();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.ReconnectDelaySeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        LogCommunicationTaskStopped();
    }

    private static bool IsUnauthorized(WebSocketException ex)
    {
        return ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True for loopback targets (localhost or a loopback IP), which are exempt from
    /// the TLS requirement so local development keeps working over ws://.
    /// </summary>
    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }


    /// <summary>
    /// Resolves the auth token to connect with: configured token, then the enrolled
    /// identity, then a fresh enrollment with the enroll token.
    /// </summary>
    private async Task<string> ResolveTokenAsync(CancellationToken stoppingToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Token))
        {
            return _settings.Token;
        }

        if (!string.IsNullOrWhiteSpace(_identityStore.AuthToken))
        {
            return _identityStore.AuthToken;
        }

        if (!string.IsNullOrWhiteSpace(_settings.EnrollToken))
        {
            return await _enrollmentClient.EnrollAsync(stoppingToken);
        }

        return string.Empty;
    }

    private async Task ConnectAndRunAsync(CancellationToken stoppingToken)
    {
        var nodeName = string.IsNullOrWhiteSpace(_settings.NodeName)
            ? Environment.MachineName
            : _settings.NodeName;

        var token = await ResolveTokenAsync(stoppingToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("No auth token or enroll token available");
        }

        using var socket = new ClientWebSocket();
        var uri = new Uri($"{_settings.ServerUrl.TrimEnd('/')}?token={Uri.EscapeDataString(token)}");

        LogConnecting(_settings.ServerUrl, nodeName);
        await socket.ConnectAsync(uri, stoppingToken);
        _socket = socket;

        LogConnected();

        try
        {
            await SendAsync(new WebSocketMessage
            {
                Type = MessageType.NodeRegistration,
                Data = new NodeRegistrationMessage
                {
                    NodeId = string.Empty,
                    NodeName = nodeName,
                    NodeVersion = NodeInfo.Version,
                    MaxConcurrentTests = Math.Max(1, _settings.MaxConcurrentTests),
                    HeartbeatIntervalSeconds = Math.Max(1, _settings.HeartbeatIntervalSeconds),
                    DefaultTestTimeoutSeconds = Math.Max(1, _settings.DefaultTestTimeoutSeconds),
                    MaxTestTimeoutSeconds = Math.Max(1, _settings.MaxTestTimeoutSeconds),
                    ReconnectDelaySeconds = Math.Max(1, _settings.ReconnectDelaySeconds),
                    InternalIpv4 = _addressState.InternalIpv4,
                    InternalIpv6 = _addressState.InternalIpv6,
                    ExternalIpv4 = _addressState.ExternalIpv4,
                    ExternalIpv6 = _addressState.ExternalIpv6
                }
            });

            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var heartbeatTask = HeartbeatLoopAsync(connectionCts.Token);

            await ReceiveLoopAsync(socket, connectionCts.Token);

            connectionCts.Cancel();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
                // heartbeat loop ends on cancellation
            }
        }
        finally
        {
            _socket = null;
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", closeCts.Token);
                }
                catch (Exception ex)
                {
                    LogCloseHandshakeFailed(ex);
                }
            }
        }
    }

    /// <summary>
    /// Handles the server's hello message: logs the server version (and a notice when it
    /// changed since the last connection), then checks compatibility. An unsupported server
    /// version closes the connection unless AllowUnsupportedServerVersion is enabled.
    /// </summary>
    private async Task HandleServerHelloAsync(ServerHelloMessage? hello)
    {
        var serverVersion = hello?.ServerVersion;
        if (string.IsNullOrWhiteSpace(serverVersion))
        {
            LogServerHelloWithoutVersion();
            return;
        }

        // The server version is logged on every (re)connection, plus an explicit
        // notice when it changed since the last connection
        var previous = _identityStore.LastServerVersion;
        if (previous != null && !string.Equals(previous, serverVersion, StringComparison.Ordinal))
        {
            LogServerVersionChanged(previous, serverVersion);
        }
        LogConnectedServerVersion(serverVersion);
        _identityStore.SaveServerVersion(serverVersion);

        // Observability policy from the server: log shipping gate and the
        // local logging default, which this node may override in its own config
        var policy = hello!;
        ApplyObservabilityPolicy(policy.LogShippingEnabled, policy.NodeLocalLoggingEnabled);

        // Supported servers are within the same major.minor version as this node
        if (ObiconVersions.IsSupported(serverVersion, NodeInfo.Version))
        {
            return;
        }

        if (_settings.AllowUnsupportedServerVersion)
        {
            LogUnsupportedVersionAllowed(serverVersion, NodeInfo.Version);
            return;
        }

        LogUnsupportedServerVersion(serverVersion, NodeInfo.Version);

        var socket = _socket;
        if (socket != null)
        {
            try
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Server version not supported", closeCts.Token);
            }
            catch (Exception ex)
            {
                LogCloseAfterUnsupportedVersionFailed(ex);
            }
        }

        _socket = null;
    }

    /// <summary>
    /// Applies the server's observability policy, announced on connect and on every
    /// runtime change. Every changed setting is logged before it takes effect, so the
    /// notice is visible even when the change itself mutes output. The node's
    /// LocalLoggingEnabled override wins over the server's default; a disabled local
    /// logging policy mutes only test-related output, not lifecycle logs.
    /// </summary>
    private void ApplyObservabilityPolicy(bool logShippingEnabled, bool nodeLocalLoggingEnabled)
    {
        var shippingChanged = _loggingState.ServerAllowsLogShipping != logShippingEnabled;
        if (shippingChanged)
        {
            LogLogShippingSettingChanged(
                logShippingEnabled ? "enabled" : "disabled",
                _loggingState.ServerAllowsLogShipping ? "enabled" : "disabled");
        }

        _loggingState.ServerAllowsLogShipping = logShippingEnabled;
        _loggingState.ServerLocalLoggingEnabled = nodeLocalLoggingEnabled;

        var newLocalLogging = _settings.LocalLoggingEnabled ?? nodeLocalLoggingEnabled;
        if (_loggingState.LastAppliedLocalLogging != newLocalLogging)
        {
            LogLocalLoggingSettingChanged(
                newLocalLogging ? "enabled" : "disabled",
                _loggingState.LastAppliedLocalLogging ? "enabled" : "disabled",
                _settings.LocalLoggingEnabled == false
                    ? "; disabled by this node's own configuration"
                    : string.Empty);
        }

        _loggingState.ApplyLocalLoggingPolicy(_settings.LocalLoggingEnabled, nodeLocalLoggingEnabled);
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _settings.HeartbeatIntervalSeconds));
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            Metrics.NodeMetrics.Heartbeat();
            await SendAsync(new WebSocketMessage
            {
                Type = MessageType.NodeHeartbeat,
                Data = new NodeHeartbeatMessage { NodeId = string.Empty, Timestamp = DateTime.UtcNow }
            });

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            string json;
            using var payload = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    LogServerClosedConnection(result.CloseStatusDescription);
                    return;
                }
                payload.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            json = Encoding.UTF8.GetString(payload.ToArray());
            await HandleMessageAsync(json);
        }
    }

    private async Task HandleMessageAsync(string json)
    {
        WebSocketMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<WebSocketMessage>(json);
        }
        catch (JsonException ex)
        {
            LogMalformedMessage(ex, json);
            return;
        }

        if (message == null)
        {
            return;
        }

        LogReceivedMessage(message.Type);

        switch (message.Type)
        {
            case MessageType.ServerHello when message.Data is JsonElement helloElement:
                await HandleServerHelloAsync(helloElement.Deserialize<ServerHelloMessage>());
                break;

            case MessageType.ServerPolicyUpdate when message.Data is JsonElement policyElement:
                var update = policyElement.Deserialize<ServerPolicyUpdateMessage>();
                if (update != null)
                {
                    ApplyObservabilityPolicy(update.LogShippingEnabled, update.NodeLocalLoggingEnabled);
                    LogPolicyUpdatedOnTheFly(update.LogShippingEnabled, update.NodeLocalLoggingEnabled);
                }
                break;

            case MessageType.TestAssignment when message.Data is JsonElement element:
                {
                    var assignment = element.Deserialize<TestAssignmentMessage>();
                    if (assignment == null)
                    {
                        LogUnparseableAssignment(json);
                        return;
                    }
                    // Marked as test activity so the local logging policy can mute just these,
                    // without silencing this class's lifecycle logs; the scope flows into the
                    // event properties like the executor's JobId scope does
                    using var _ = _logger.BeginScope(new Dictionary<string, object>
                    {
                        [NodeLoggingState.TestActivityProperty] = true
                    });
                    LogAssignedJob(assignment.JobId, assignment.TestType, assignment.Target);
                    await _testExecutor.ExecuteAssignmentAsync(assignment);
                    break;
                }

            default:
                LogIgnoringMessage(message.Type);
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropping message {MessageType}: not connected to server")]
    private partial void LogDroppingMessage(MessageType messageType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send {MessageType} to server")]
    private partial void LogSendFailed(Exception exception, MessageType messageType);

    [LoggerMessage(Level = LogLevel.Error, Message = "No token configured. Set Node:Token or Node:EnrollToken in appsettings.json, or the Node__Token / Node__EnrollToken environment variables")]
    private partial void LogNoTokenConfigured();

    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid ServerUrl '{ServerUrl}'. It must be an absolute ws:// or wss:// URL")]
    private partial void LogInvalidServerUrl(string serverUrl);

    [LoggerMessage(Level = LogLevel.Error, Message = "ServerUrl '{ServerUrl}' is an unencrypted ws:// connection, but TLS is required by default. Use a wss:// URL, or set Node:RequireTls to false to override")]
    private partial void LogTlsRequired(string serverUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HeartbeatIntervalSeconds, MaxConcurrentTests and DefaultTestTimeoutSeconds should be at least 1; they are clamped to 1")]
    private partial void LogClampedSettings();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server rejected the stored token; re-enrolling")]
    private partial void LogTokenRejected();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection to server lost")]
    private partial void LogConnectionLost(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnecting in {Delay}s")]
    private partial void LogReconnecting(int delay);

    [LoggerMessage(Level = LogLevel.Information, Message = "Communication task stopped")]
    private partial void LogCommunicationTaskStopped();

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to {ServerUrl} as {NodeName}")]
    private partial void LogConnecting(string serverUrl, string nodeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to primary server")]
    private partial void LogConnected();

    [LoggerMessage(Level = LogLevel.Debug, Message = "WebSocket close handshake failed")]
    private partial void LogCloseHandshakeFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server hello did not include a version; skipping the compatibility check")]
    private partial void LogServerHelloWithoutVersion();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server version changed: v{PreviousVersion} is now v{ServerVersion}")]
    private partial void LogServerVersionChanged(string? previousVersion, string? serverVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Obicon server v{ServerVersion}")]
    private partial void LogConnectedServerVersion(string? serverVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server v{ServerVersion} is outside this node's supported range (same major.minor as v{NodeVersion}); AllowUnsupportedServerVersion is enabled, continuing anyway")]
    private partial void LogUnsupportedVersionAllowed(string? serverVersion, string nodeVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Server v{ServerVersion} is not supported by this node (v{NodeVersion}, same major.minor required). Disconnecting; upgrade the node or the server, or set Node:AllowUnsupportedServerVersion to continue anyway")]
    private partial void LogUnsupportedServerVersion(string? serverVersion, string nodeVersion);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Close handshake after unsupported server version failed")]
    private partial void LogCloseAfterUnsupportedVersionFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting changed: server log shipping is now {New} (was {Old})")]
    private partial void LogLogShippingSettingChanged(string @new, string @old);

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting changed: local test logging is now {New} (was {Old}){Override}")]
    private partial void LogLocalLoggingSettingChanged(string @new, string @old, string @override);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server closed the connection: {Description}")]
    private partial void LogServerClosedConnection(string? description);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received malformed message: {Json}")]
    private partial void LogMalformedMessage(Exception exception, string json);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received {MessageType} from server")]
    private partial void LogReceivedMessage(MessageType messageType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server updated its policy on the fly: logShipping={LogShipping} localLogging={LocalLogging}")]
    private partial void LogPolicyUpdatedOnTheFly(bool logShipping, bool localLogging);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Test assignment could not be parsed: {Json}")]
    private partial void LogUnparseableAssignment(string json);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigned job {JobId}: {TestType} against {Target}")]
    private partial void LogAssignedJob(string jobId, TestType testType, string target);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring message type {MessageType}")]
    private partial void LogIgnoringMessage(MessageType messageType);
}
