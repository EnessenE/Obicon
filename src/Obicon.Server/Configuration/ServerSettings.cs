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
    /// JSON array of the test types this server offers, e.g. ["Ping","Http","Dns"].
    /// Empty means all types. Default: empty string.
    /// </summary>
    public string EnabledTestTypes { get; set; } = string.Empty;

    /// <summary>
    /// If enabled, log entries received from nodes are written to the server's own console. Default: false.
    /// </summary>
    public bool ShipNodeLogsToConsole { get; set; }

    /// <summary>
    /// If enabled, finished test runs are exported as test metrics on /metrics. Default: true.
    /// </summary>
    public bool TestMetricsEnabled { get; set; } = true;

    /// <summary>
    /// JSON array of the labels attached to the exported test metrics, e.g.
    /// ["test_type","node_name","node_labels"]. "test_id" and the counter's "status"
    /// are always attached. Default: "["test_type","test_name","node_name","node_labels"]".
    /// </summary>
    public string TestMetricsLabels { get; set; } = "[\"test_type\",\"test_name\",\"node_name\",\"node_labels\"]";

    /// <summary>
    /// How finished test jobs are stored: Full, MetadataOnly, or None. Default: "Full".
    /// </summary>
    public string TestResultStorageMode { get; set; } = "Full";

    /// <summary>
    /// Days a finished test job stays in the database before the retention sweep
    /// deletes it; history beyond this window lives in the user's metric store. Default: 30.
    /// </summary>
    public int JobRetentionDays { get; set; } = 30;
}
