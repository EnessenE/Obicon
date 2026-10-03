using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Obicon.Server.WebSockets;
using Obicon.Shared.Models.Messages;

namespace Obicon.Server.Services;

/// <summary>
/// Pushes the node-facing policy to all connected nodes when it changes at runtime,
/// so nodes apply new settings on the fly instead of waiting for a reconnect.
/// </summary>
public partial class NodePolicyBroadcaster
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
    public async Task BroadcastAsync(bool logShippingEnabled, bool nodeLocalLoggingEnabled, bool externalIpResolvingEnabled)
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
                NodeLocalLoggingEnabled = nodeLocalLoggingEnabled,
                ExternalIpResolvingEnabled = externalIpResolvingEnabled
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
                LogPolicyDeliveryFailed(ex, connection.NodeId);
            }
        }

        if (delivered > 0)
        {
            LogPolicyDelivered(delivered, logShippingEnabled, nodeLocalLoggingEnabled, externalIpResolvingEnabled);
            Metrics.ServerMetrics.Action("policy_updated");
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not deliver policy update to node {NodeId}")]
    private partial void LogPolicyDeliveryFailed(System.Exception exception, string nodeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Policy update delivered to {Count} node(s): logShipping={LogShipping} localLogging={LocalLogging} externalIpResolving={ExternalIpResolving}")]
    private partial void LogPolicyDelivered(int count, bool logShipping, bool localLogging, bool externalIpResolving);
}
