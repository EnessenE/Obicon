namespace Obicon.Server.Models;

/// <summary>
/// One item of a list-typed server setting override, in the setting's order.
/// Changing the setting replaces the whole set of rows for its key.
/// </summary>
public class ServerSettingListValue
{
    /// <summary>
    /// Key of the setting, matching the ServerSettings property name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based position of the item within the list.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// The item's value as text; converted to the setting's item type on read.
    /// </summary>
    public string Item { get; set; } = string.Empty;
}
