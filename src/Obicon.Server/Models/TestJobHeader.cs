namespace Obicon.Server.Models;

/// <summary>
/// A custom HTTP header copied onto a job from its test at enqueue time, so the
/// dispatched assignment is immune to later test edits. Rows cascade on job deletion.
/// Default: empty table.
/// </summary>
public class TestJobHeader
{
    /// <summary>
    /// ID of the job the header belongs to. Part of the primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The header name. Part of the primary key. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The header value. Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
