namespace Obicon.Server.Configuration;

public class ServerSettings
{
    /// <summary>
    /// Authorization header value required for API access. Default: "secureobiconkey".
    /// </summary>
    public string AuthHeader { get; set; } = "secureobiconkey";

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

    /// <summary>
    /// If enabled, the server accepts nodes whose version is outside the supported range
    /// (same major.minor) instead of closing their connection. Default: false.
    /// </summary>
    public bool AllowUnsupportedNodeVersions { get; set; }

    /// <summary>
    /// If enabled, nodes may ship their log entries to the server. Default: false.
    /// </summary>
    public bool NodeLogShippingEnabled { get; set; }

    /// <summary>
    /// Default policy for whether nodes write logs locally; nodes can override it. Default: true.
    /// </summary>
    public bool NodeLocalLoggingEnabled { get; set; } = true;

    /// <summary>
    /// Whether nodes may resolve their external (public) addresses via the configured
    /// check services. Default: false.
    /// </summary>
    public bool NodeExternalIpResolvingEnabled { get; set; }

    /// <summary>
    /// If enabled, log entries received from nodes are written to the server's own console. Default: false.
    /// </summary>
    public bool ShipNodeLogsToConsole { get; set; }

    /// <summary>
    /// If enabled, finished test runs are exported as test metrics on /metrics. Default: true.
    /// </summary>
    public bool TestMetricsEnabled { get; set; } = true;

    /// <summary>
    /// If enabled, the executing node's labels are attached to the exported test metrics. Default: true.
    /// </summary>
    public bool TestMetricsIncludeNodeLabels { get; set; } = true;
}
