using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services;

/// <summary>
/// Dedicated communication task: maintains the WebSocket connection to the primary server,
/// registers the node, sends heartbeats, dispatches test assignments to the executor,
/// and reconnects with a delay on disconnect.
/// </summary>
public class ServerConnection : BackgroundService, IServerConnection
{
    private readonly NodeSettings _settings;
    private readonly ITestExecutor _testExecutor;
    private readonly ILogger<ServerConnection> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private volatile ClientWebSocket? _socket;

    public ServerConnection(
        IOptions<NodeSettings> settings,
        ITestExecutor testExecutor,
        ILogger<ServerConnection> logger)
    {
        _settings = settings.Value;
        _testExecutor = testExecutor;
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
            _logger.LogDebug("Dropping message {MessageType}: not connected to server", message.Type);
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
            _logger.LogWarning(ex, "Failed to send {MessageType} to server", message.Type);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Token))
        {
            _logger.LogError("No token configured. Set Node:Token in appsettings.json or the Node__Token environment variable");
        }

        if (!Uri.TryCreate(_settings.ServerUrl, UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != "ws" && serverUri.Scheme != "wss"))
        {
            _logger.LogError("Invalid ServerUrl '{ServerUrl}'. It must be an absolute ws:// or wss:// URL", _settings.ServerUrl);
            return;
        }

        if (_settings.HeartbeatIntervalSeconds < 1 || _settings.MaxConcurrentTests < 1 || _settings.DefaultTestTimeoutSeconds < 1)
        {
            _logger.LogWarning("HeartbeatIntervalSeconds, MaxConcurrentTests and DefaultTestTimeoutSeconds should be at least 1; they are clamped to 1");
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Connection to server lost");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnecting in {Delay}s", _settings.ReconnectDelaySeconds);
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

        _logger.LogInformation("Communication task stopped");
    }

    private async Task ConnectAndRunAsync(CancellationToken stoppingToken)
    {
        var nodeName = string.IsNullOrWhiteSpace(_settings.NodeName)
            ? Environment.MachineName
            : _settings.NodeName;

        using var socket = new ClientWebSocket();
        var uri = new Uri($"{_settings.ServerUrl.TrimEnd('/')}?token={Uri.EscapeDataString(_settings.Token)}");

        _logger.LogInformation("Connecting to {ServerUrl} as {NodeName}", _settings.ServerUrl, nodeName);
        await socket.ConnectAsync(uri, stoppingToken);
        _socket = socket;

        _logger.LogInformation("Connected to primary server");

        try
        {
            await SendAsync(new WebSocketMessage
            {
                Type = MessageType.NodeRegistration,
                Data = new NodeRegistrationMessage { NodeId = string.Empty, NodeName = nodeName }
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
                    _logger.LogDebug(ex, "WebSocket close handshake failed");
                }
            }
        }
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
                    _logger.LogInformation("Server closed the connection: {Description}", result.CloseStatusDescription);
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
            _logger.LogWarning(ex, "Received malformed message: {Json}", json);
            return;
        }

        if (message == null)
        {
            return;
        }

        _logger.LogDebug("Received {MessageType} from server", message.Type);

        switch (message.Type)
        {
            case MessageType.TestAssignment when message.Data is JsonElement element:
                var assignment = element.Deserialize<TestAssignmentMessage>();
                if (assignment == null)
                {
                    _logger.LogWarning("Test assignment could not be parsed: {Json}", json);
                    return;
                }
                _logger.LogInformation("Assigned job {JobId}: {TestType} against {Target}",
                    assignment.JobId, assignment.TestType, assignment.Target);
                await _testExecutor.ExecuteAssignmentAsync(assignment);
                break;

            default:
                _logger.LogDebug("Ignoring message type {MessageType}", message.Type);
                break;
        }
    }
}
