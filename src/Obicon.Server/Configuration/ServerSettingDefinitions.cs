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
            Default = "uwu"
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
            Description = "Read-only, derived from the lowest FrequencyPresetsSeconds preset. The test scheduler wakes this often to scan for due tests. Each wake is one SQLite query plus an in-memory scan, after which the loop sleeps (Task.Delay), so the CPU cost is one short database burst per wake - a lower interval means proportionally more wakes per hour (e.g. 10 seconds = 360 scans/hour). Adjust FrequencyPresetsSeconds to change it.",
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
            Key = "ShipNodeLogsToConsole",
            Description = "If enabled, log entries received from nodes are written to the server's own console and log, tagged with the node's identity. Requires NodeLogShippingEnabled; otherwise nothing is received.",
            ValueType = typeof(bool),
            Default = "false",
            Group = "Observability"
        }
    };
}
