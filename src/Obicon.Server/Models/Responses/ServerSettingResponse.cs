namespace Obicon.Server.Models.Responses;

/// <summary>
/// A server setting as exposed by the API: effective value, description, and whether configuration forces it.
/// </summary>
public class ServerSettingResponse
{
    /// <summary>
    /// Key of the setting, matching the ServerSettings property name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the setting does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Effective value of the setting as string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if appsettings or an environment variable pins this setting, making it read-only.
    /// </summary>
    public bool IsForced { get; set; }

    /// <summary>
    /// Where the effective value comes from: "Configuration (forced)", "Database", or "Default".
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
