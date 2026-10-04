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

/// <summary>
/// Pool membership: one row per node that belongs to a pool. Default: empty table.
/// </summary>
public class PoolMember
{
    /// <summary>
    /// ID of the pool. Part of the primary key, cascades on pool deletion.
    /// </summary>
    public Guid PoolId { get; set; }

    /// <summary>
    /// ID of the member node. Part of the primary key, cascades on node deletion.
    /// </summary>
    public Guid NodeId { get; set; }
}
