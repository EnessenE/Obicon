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
        }
    };
}
