namespace Obicon.Server.Models;

/// <summary>
/// Runtime override of a server setting, stored in SQLite.
/// A setting present in appsettings or environment variables is forced and cannot be overridden.
/// </summary>
public class ServerSettingValue
{
    /// <summary>
    /// Key of the setting, matching the ServerSettings property name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Value of the setting as string; converted to the setting's type on read. Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the value was last changed.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
