namespace Obicon.Server.Models;

/// <summary>
/// An operating setting a node reported about itself on registration (e.g.
/// MaxConcurrentTests), stored as one row per key. Default: empty table.
/// </summary>
public class NodeReportedSetting
{
    /// <summary>
    /// ID of the node that reported the setting. Part of the primary key, cascades on node deletion.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// The setting name, e.g. "MaxConcurrentTests". Part of the primary key. Default: empty string.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The reported value. Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
