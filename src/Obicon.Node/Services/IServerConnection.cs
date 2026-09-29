using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services;

/// <summary>
/// Connection to the primary server. Owned by the dedicated communication task.
/// </summary>
public interface IServerConnection
{
    /// <summary>
    /// Indicates if the WebSocket connection to the primary server is currently open.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Sends a message to the primary server. Dropped with a warning if not connected.
    /// </summary>
    /// <param name="message">The WebSocket message to send.</param>
    Task SendAsync(WebSocketMessage message);
}
