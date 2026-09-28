namespace Obicon.Server.Configuration;

public class ServerSettings
{
    /// <summary>
    /// Authorization header value required for API access. Default: "uwu".
    /// </summary>
    public string AuthHeader { get; set; } = "uwu";

    /// <summary>
    /// Maximum timeout in seconds for test execution. Default: 60.
    /// </summary>
    public int MaxTestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout in seconds for node WebSocket connections. Default: 30.
    /// </summary>
    public int NodeConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// URL path for WebSocket connections. Default: "/ws/nodes".
    /// </summary>
    public string WebSocketPath { get; set; } = "/ws/nodes";
}
