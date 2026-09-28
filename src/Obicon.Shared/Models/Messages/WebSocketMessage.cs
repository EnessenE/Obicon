using System.Text.Json.Serialization;

namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Base WebSocket message with type discriminator for routing.
/// </summary>
public class WebSocketMessage
{
    /// <summary>
    /// Message type for routing. See <see cref="MessageType"/> for available types.
    /// </summary>
    [JsonPropertyName("type")]
    public MessageType Type { get; set; }

    /// <summary>
    /// Message data payload. Type varies by MessageType.
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
