using System.Text.Json.Serialization;

namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Generic WebSocket message with typed data payload.
/// </summary>
/// <typeparam name="T">Type of the data payload.</typeparam>
public class WebSocketMessage<T> : WebSocketMessage
{
    /// <summary>
    /// Strongly-typed message data payload.
    /// </summary>
    [JsonPropertyName("data")]
    public new T? Data { get; set; }
}
