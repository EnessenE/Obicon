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
