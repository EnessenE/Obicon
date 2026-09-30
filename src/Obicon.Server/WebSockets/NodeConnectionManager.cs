using System.Net.WebSockets;

namespace Obicon.Server.WebSockets;

/// <summary>
/// Manages active WebSocket connections from nodes.
/// </summary>
public class NodeConnectionManager
{
    private readonly Dictionary<string, NodeConnection> _connections = new();
    private readonly object _lock = new();

    /// <summary>
    /// Attempts to add a new node connection. Returns false if node is already connected.
    /// </summary>
    /// <param name="nodeId">Unique identifier of the node.</param>
    /// <param name="nodeName">Human-readable name of the node.</param>
    /// <param name="socket">The WebSocket connection.</param>
    /// <returns>True if connection was added, false if node already connected.</returns>
    public bool TryAddConnection(string nodeId, string nodeName, WebSocket socket)
    {
        lock (_lock)
        {
            if (_connections.ContainsKey(nodeId))
                return false;

            _connections[nodeId] = new NodeConnection
            {
                NodeId = nodeId,
                NodeName = nodeName,
                Socket = socket,
                ConnectedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            return true;
        }
    }

    /// <summary>
    /// Attempts to remove a node connection.
    /// </summary>
    /// <param name="nodeId">Unique identifier of the node.</param>
    /// <returns>True if connection was removed, false if not found.</returns>
    public bool TryRemoveConnection(string nodeId)
    {
        lock (_lock)
        {
            return _connections.Remove(nodeId);
        }
    }

    /// <summary>
    /// Gets a node connection by node ID.
    /// </summary>
    /// <param name="nodeId">Unique identifier of the node.</param>
    /// <returns>The connection or null if not found.</returns>
    public NodeConnection? GetConnection(string nodeId)
    {
        lock (_lock)
        {
            _connections.TryGetValue(nodeId, out var connection);
            return connection;
        }
    }

    /// <summary>
    /// Gets all active node connections.
    /// </summary>
    /// <returns>Collection of all active connections.</returns>
    public IEnumerable<NodeConnection> GetAllConnections()
    {
        lock (_lock)
        {
            return _connections.Values.ToList();
        }
    }

    /// <summary>
    /// Updates the last seen timestamp for a node.
    /// </summary>
    /// <param name="nodeId">Unique identifier of the node.</param>
    public void UpdateLastSeen(string nodeId)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(nodeId, out var connection))
            {
                connection.LastSeenAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Closes a node's live WebSocket connection, e.g. after its token was regenerated
    /// or after the node was deleted.
    /// </summary>
    /// <param name="nodeId">Unique identifier of the node.</param>
    /// <param name="reason">Close description sent to the node.</param>
    public async Task DisconnectNodeAsync(string nodeId, string reason = "Authentication token was regenerated")
    {
        NodeConnection? connection;
        lock (_lock)
        {
            if (!_connections.TryGetValue(nodeId, out connection))
            {
                return;
            }
        }

        try
        {
            using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await connection.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, reason, closeCts.Token);
        }
        catch (Exception)
        {
            // The receive loop in the middleware cleans up either way
        }
    }
}
