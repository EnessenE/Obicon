using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Obicon.Server.WebSockets;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Server.Services;

/// <summary>
/// Pushes the node-facing policy to all connected nodes when it changes at runtime,
/// so nodes apply new settings on the fly instead of waiting for a reconnect.
/// </summary>
public class NodePolicyBroadcaster
{
    private readonly NodeConnectionManager _connectionManager;
    private readonly ILogger<NodePolicyBroadcaster> _logger;

    public NodePolicyBroadcaster(NodeConnectionManager connectionManager, ILogger<NodePolicyBroadcaster> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Sends the current observability policy to every connected node.
    /// </summary>
    public async Task BroadcastAsync(bool logShippingEnabled, bool nodeLocalLoggingEnabled)
    {
        var connections = _connectionManager.GetAllConnections().ToList();
        if (connections.Count == 0)
        {
            return;
        }

        var message = new WebSocketMessage
        {
            Type = MessageType.ServerPolicyUpdate,
            Data = new ServerPolicyUpdateMessage
            {
                LogShippingEnabled = logShippingEnabled,
                NodeLocalLoggingEnabled = nodeLocalLoggingEnabled
            }
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var delivered = 0;
        foreach (var connection in connections)
        {
            if (connection.Socket.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                await connection.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                delivered++;
            }
            catch (Exception ex)
            {
                // The node may have dropped mid-broadcast; its reconnect will pick the policy up from the hello
                _logger.LogDebug(ex, "Could not deliver policy update to node {NodeId}", connection.NodeId);
            }
        }

        if (delivered > 0)
        {
            _logger.LogInformation("Policy update delivered to {Count} node(s): logShipping={LogShipping} localLogging={LocalLogging}",
                delivered, logShippingEnabled, nodeLocalLoggingEnabled);
            Metrics.ServerMetrics.Action("policy_updated");
        }
    }
}
