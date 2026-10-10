namespace Obicon.Server.Models;

/// <summary>
/// A label attached to a node, stored as one row per label. Default: empty table.
/// </summary>
public class NodeLabel
{
    /// <summary>
    /// ID of the node the label belongs to. Part of the primary key, cascades on node deletion.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// The label text. Part of the primary key. Default: empty string.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
