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
    /// Host the Prometheus metrics listener binds to. Default: "localhost"; use "+" to expose
    /// metrics outside the machine or container.
    /// </summary>
    public string MetricsHost { get; set; } = "localhost";

    /// <summary>
    /// Port the Prometheus metrics listener binds to. Default: 9464.
    /// </summary>
    public int MetricsPort { get; set; } = 9464;

    /// <summary>
    /// Enroll token to register this node with the server when no auth token is configured.
    /// Default: empty string.
    /// </summary>
    public string EnrollToken { get; set; } = string.Empty;

    /// <summary>
    /// Labels this node attaches to itself when enrolling. Default: empty list.
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// Names of pools this node puts itself into when enrolling. Default: empty list.
    /// </summary>
    public List<string> Pools { get; set; } = new();

    /// <summary>
    /// If enabled, this node stays connected even when the server reports a version outside
    /// the supported range (same major.minor as the node). Default: false.
    /// </summary>
    public bool AllowUnsupportedServerVersion { get; set; }

    /// <summary>
    /// If enabled, this node ships its log entries to the server while the server allows it
    /// (NodeLogShippingEnabled on the server). The node-side switch to opt out. Default: true.
    /// </summary>
    public bool LogShippingEnabled { get; set; } = true;

    /// <summary>
    /// Local override for whether this node logs to its own console: null follows the server's
    /// NodeLocalLoggingEnabled policy announced on connect, true always logs locally,
    /// false never logs locally. Default: null.
    /// </summary>
    public bool? LocalLoggingEnabled { get; set; }

    /// <summary>
    /// Minimum level of entries shipped to the server: Debug, Information, Warning, or Error.
    /// Default: "Information".
    /// </summary>
    public string LogShippingMinLevel { get; set; } = "Information";

    /// <summary>
    /// How often the node refreshes its internal and external IP addresses, in minutes.
    /// Changes are reported to the server immediately. Default: 30.
    /// </summary>
    public int IpCheckIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// URL of the service the node asks for its external (public) IPv4 address; it must
    /// respond with the address in plain text or JSON. Default: "https://checkip.amazonaws.com".
    /// </summary>
    public string ExternalIpCheckUrl { get; set; } = "https://checkip.amazonaws.com";

    /// <summary>
    /// URL of the service the node asks for its external (public) IPv6 address; it must
    /// respond with the address in plain text or JSON. Null or unreachable means IPv6
    /// is reported as unavailable. Default: "https://api6.ipify.org".
    /// </summary>
    public string ExternalIpCheckUrlIpv6 { get; set; } = "https://api6.ipify.org";
}
