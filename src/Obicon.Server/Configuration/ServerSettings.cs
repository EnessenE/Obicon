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

    /// <summary>
    /// Indicates if nodes can register themselves with an enroll token. Default: false.
    /// </summary>
    public bool NodeAutoEnrollmentEnabled { get; set; }

    /// <summary>
    /// Comma-separated list of test frequencies, in seconds, offered in the UI and accepted when
    /// creating or editing a test. Default: "10,30,60,120,300,600,3600".
    /// </summary>
    public string FrequencyPresetsSeconds { get; set; } = "10,30,60,120,300,600,3600";
}
