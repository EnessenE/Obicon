namespace Obicon.Node.Configuration;

/// <summary>
/// Configuration for the node, bound from the "Node" section of appsettings.json.
/// Can be overridden with environment variables, e.g. Node__Token.
/// </summary>
public class NodeSettings
{
    /// <summary>
    /// WebSocket URL of the primary server to connect to. Default: "ws://localhost:5000/ws/nodes".
    /// </summary>
    public string ServerUrl { get; set; } = "ws://localhost:5000/ws/nodes";

    /// <summary>
    /// Authentication token of this node, created on the primary server. Default: empty string.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of this node shown on the server. Default: machine name.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// How often to send a heartbeat to the primary server, in seconds. Default: 1.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Maximum number of tests executing at the same time. Default: 4.
    /// </summary>
    public int MaxConcurrentTests { get; set; } = 4;

    /// <summary>
    /// Default maximum execution time per test in seconds. Default: 60.
    /// </summary>
    public int DefaultTestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Upper limit for a test timeout in seconds. Assignments above this are capped. Default: 60.
    /// </summary>
    public int MaxTestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Delay before reconnecting to the server after a disconnect, in seconds. Default: 5.
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// URL prefix the health endpoint listens on. Default: "http://localhost:8080/".
    /// </summary>
    public string HealthUrlPrefix { get; set; } = "http://localhost:8080/";

    /// <summary>
    /// How often the monitoring task logs node statistics, in seconds. Default: 5.
    /// </summary>
    public int MonitoringIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// URL prefix the Prometheus metrics listener binds to. Default: "http://localhost:9464/".
    /// </summary>
    public string MetricsUrlPrefix { get; set; } = "http://localhost:9464/";
}
