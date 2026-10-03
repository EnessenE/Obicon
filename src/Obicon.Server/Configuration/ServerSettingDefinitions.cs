namespace Obicon.Server.Configuration;

/// <summary>
/// Description of one known server setting: its key, meaning, type, and default value.
/// </summary>
public class ServerSettingDefinition
{
    /// <summary>
    /// Key of the setting, matching the ServerSettings property name. Default: empty string.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description shown in the API and UI. Default: empty string.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Value type of the setting: string, int, or bool.
    /// </summary>
    public Type ValueType { get; init; } = typeof(string);

    /// <summary>
    /// Read-only settings are derived from other settings instead of being stored;
    /// they cannot be changed through the API or UI. Default: false.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Section the setting is displayed under in the settings UI, e.g. "General" or "Observability".
    /// Default: "General".
    /// </summary>
    public string Group { get; init; } = "General";

    /// <summary>
    /// Default value as string, used when nothing is configured or stored. Default: empty string.
    /// </summary>
    public string Default { get; init; } = string.Empty;
}

/// <summary>
/// Registry of all known server settings. Add new settings here and to the ServerSettings class.
/// Every setting must have a description - it is shown in the settings UI and API.
/// </summary>
public static class ServerSettingDefinitions
{
    /// <summary>
    /// All known server settings.
    /// </summary>
    public static readonly List<ServerSettingDefinition> All = new()
    {
        new ServerSettingDefinition
        {
            Key = "AuthHeader",
            Description = "Value required in the Authorization header to access the API. Change this to use a real secret.",
            ValueType = typeof(string),
            Default = "secureobiconkey"
        },
        new ServerSettingDefinition
        {
            Key = "MaxTestTimeoutSeconds",
            Description = "Upper limit for a test timeout in seconds. Assignments above this are capped.",
            ValueType = typeof(int),
            Default = "60"
        },
        new ServerSettingDefinition
        {
            Key = "NodeConnectionTimeoutSeconds",
            Description = "Seconds after which a node connection without heartbeats is considered stale. Reserved for future use.",
            ValueType = typeof(int),
            Default = "30"
        },
        new ServerSettingDefinition
        {
            Key = "WebSocketPath",
            Description = "URL path nodes connect their WebSocket to.",
            ValueType = typeof(string),
            Default = "/ws/nodes"
        },
        new ServerSettingDefinition
        {
            Key = "NodeAutoEnrollmentEnabled",
            Description = "If enabled, nodes can register themselves with a valid enroll token, providing their name, labels, and pools. Nodes that enrolled themselves manage their own labels and pools.",
            ValueType = typeof(bool),
            Default = "false"
        },
        new ServerSettingDefinition
        {
            Key = "NoRunGraceFactor",
            Description = "A dispatched job that the node never started is marked as NoRun and removed from the queue after [test timeout] divided by this factor, in seconds.",
            ValueType = typeof(int),
            Default = "2"
        },
        new ServerSettingDefinition
        {
            Key = "EnabledTestTypes",
            Group = "General",
            Description = "JSON array of the test types this server offers, e.g. [\"Ping\",\"Http\",\"Dns\"]. Values: Ping, Traceroute, Http, Https, Tcp, Dns, Tls. An empty array or missing setting enables all types; creating, editing, or dry-running a disabled type is rejected.",
            ValueType = typeof(string),
            Default = ""
        },
        new ServerSettingDefinition
        {
            Key = "FrequencyPresetsSeconds",
            Description = "Comma-separated list of test frequencies in seconds, offered in the UI and required when creating or editing a test. Any positive seconds are allowed; e.g. \"15,45,3600\".",
            ValueType = typeof(string),
            Default = "10,30,60,120,300,600,3600"
        },
        new ServerSettingDefinition
        {
            Key = "AllowUnsupportedNodeVersions",
            Description = "If enabled, nodes reporting a version outside the supported range (same major.minor as the server) are accepted with a warning instead of being disconnected.",
            ValueType = typeof(bool),
            Default = "false"
        },
        new ServerSettingDefinition
        {
            Key = "SchedulerLoopIntervalSeconds",
            Description = "Read-only, derived from the lowest FrequencyPresetsSeconds preset. The test scheduler wakes this often to scan for due tests. Each wake is one database query plus an in-memory scan, after which the loop sleeps (Task.Delay), so the CPU cost is one short database burst per wake - a lower interval means proportionally more wakes per hour (e.g. 10 seconds = 360 scans/hour). Adjust FrequencyPresetsSeconds to change it.",
            ValueType = typeof(int),
            Default = "10",
            IsReadOnly = true
        },
        new ServerSettingDefinition
        {
            Key = "NodeLogShippingEnabled",
            Description = "If enabled, nodes may ship their log entries to the server over their WebSocket connection. When disabled, log entries arriving from nodes are dropped. Announced to every node in the server hello.",
            ValueType = typeof(bool),
            Default = "false",
            Group = "Observability"
        },
        new ServerSettingDefinition
        {
            Key = "NodeLocalLoggingEnabled",
            Description = "The server's default policy for whether nodes write logs locally (console). A node's own configuration takes precedence over this default. Announced to every node in the server hello.",
            ValueType = typeof(bool),
            Default = "true",
            Group = "Observability"
        },
        new ServerSettingDefinition
        {
            Key = "NodeExternalIpResolvingEnabled",
            Group = "Observability",
            Description = "If enabled, nodes may resolve their external (public) addresses by asking the configured check services. While disabled, nodes report their external addresses as unavailable and do not contact any check service. Announced to every node in the server hello.",
            ValueType = typeof(bool),
            Default = "false"
        },
        new ServerSettingDefinition
        {
            Key = "ShipNodeLogsToConsole",
            Description = "If enabled, log entries received from nodes are written to the server's own console and log, tagged with the node's identity. Requires NodeLogShippingEnabled; otherwise nothing is received.",
            ValueType = typeof(bool),
            Default = "false",
            Group = "Observability"
        },
        new ServerSettingDefinition
        {
            Key = "TestMetricsEnabled",
            Description = "If enabled, finished test runs are exported as obicon.tests.runs and obicon.tests.duration_ms on /metrics or via OTLP. When disabled, new runs are not recorded; already exported series persist until the server restarts.",
            ValueType = typeof(bool),
            Default = "true",
            Group = "Observability"
        },
        new ServerSettingDefinition
        {
            Key = "TestMetricsLabels",
            Group = "Observability",
            Description = "JSON array of the labels attached to the exported test metrics, e.g. [\"test_type\",\"node_name\",\"node_labels\"]. test_type (few values), test_name and node_name (human-readable, but fork series on renames), node_id (rename-stable), and node_labels (churniest: any label change on any node starts new series). \"test_id\" and the counter's \"status\" are always attached. Changing the set starts new series for subsequent runs. Requires TestMetricsEnabled.",
            ValueType = typeof(string),
            Default = "[\"test_type\",\"test_name\",\"node_name\",\"node_labels\"]"
        },
        new ServerSettingDefinition
        {
            Key = "TestResultStorageMode",
            Group = "General",
            Description = "How finished test jobs are stored in the database: Full (complete result payload), MetadataOnly (row skeleton without the payload), or None (the row is deleted on completion). Applies to every terminal status; metrics are always emitted before deletion. Anything older than JobRetentionDays exists only in your metric store.",
            ValueType = typeof(string),
            Default = "Full"
        },
        new ServerSettingDefinition
        {
            Key = "JobRetentionDays",
            Group = "General",
            Description = "Days a finished test job stays in the database before the background sweep deletes it, no exceptions - there is no archive. Want history? Check your metric store. Minimum 1; the sweep also removes jobs stuck in a live status for more than 7 days. Default: 30.",
            ValueType = typeof(int),
            Default = "30"
        }
    };
}
