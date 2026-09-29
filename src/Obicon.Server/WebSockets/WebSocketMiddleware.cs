using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using Obicon.Shared.Models.Messages;

namespace Obicon.Server.WebSockets;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly NodeConnectionManager _connectionManager;
    private readonly INodeService _nodeService;
    private readonly ILogger<WebSocketMiddleware> _logger;

    public WebSocketMiddleware(
        RequestDelegate next,
        NodeConnectionManager connectionManager,
        INodeService nodeService,
        ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _nodeService = nodeService;
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

            await HandleWebSocketConnection(node.Id.ToString(), webSocket);
            
            _connectionManager.TryRemoveConnection(node.Id.ToString());
            _logger.LogInformation("Node {NodeId} disconnected", node.Id);

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

        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }

    private async Task ProcessMessage(string nodeId, WebSocket webSocket, WebSocketMessage message)
    {
        _logger.LogDebug("Processing message type: {MessageType} from {NodeId}", message.Type, nodeId);

        switch (message.Type)
        {
            case MessageType.NodeRegistration:
                HandleNodeRegistration(nodeId);
                break;
            case MessageType.NodeHeartbeat:
                HandleNodeHeartbeat(nodeId);
                break;
            case MessageType.TestResult:
                HandleTestResult(nodeId, message);
                break;
            case MessageType.TestStatusUpdate:
                HandleTestStatusUpdate(nodeId, message);
                break;
            case MessageType.ErrorReport:
                HandleErrorReport(nodeId, message);
                break;
            default:
                _logger.LogWarning("Unknown message type: {MessageType}", message.Type);
                break;
        }
    }

    private void HandleNodeRegistration(string nodeId)
    {
        _logger.LogInformation("Node {NodeId} registered", nodeId);
        _connectionManager.UpdateLastSeen(nodeId);
    }

    private void HandleNodeHeartbeat(string nodeId)
    {
        _logger.LogDebug("Heartbeat from {NodeId}", nodeId);
        _connectionManager.UpdateLastSeen(nodeId);
    }

    private void HandleTestResult(string nodeId, WebSocketMessage message)
    {
        _logger.LogInformation("Test result from {NodeId}", nodeId);
    }

    private void HandleTestStatusUpdate(string nodeId, WebSocketMessage message)
    {
        _logger.LogInformation("Test status update from {NodeId}", nodeId);
    }

    private void HandleErrorReport(string nodeId, WebSocketMessage message)
    {
        _logger.LogError("Error report from {NodeId}", nodeId);
    }
}
