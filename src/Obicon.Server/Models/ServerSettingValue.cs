namespace Obicon.Server.Models;

/// <summary>
/// Runtime override of a scalar server setting. A setting present in appsettings
/// or environment variables is forced and cannot be overridden; list-typed setting
/// overrides are rows in <see cref="ServerSettingListValue"/> instead.
/// </summary>
public class ServerSettingValue
{
    /// <summary>
    /// Key of the setting, matching the ServerSettings property name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Value of the scalar setting as text, or the item count of a list-typed
    /// setting override; converted to the setting's type on read.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the value was last changed.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
