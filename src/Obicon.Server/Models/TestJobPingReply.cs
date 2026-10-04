namespace Obicon.Server.Models;

/// <summary>
/// One reply of a ping run.
/// </summary>
public class TestJobPingReply
{
    /// <summary>
    /// Unique identifier of the reply row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the job the reply belongs to. Cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Position of the reply within the run, 0-based; orders the replies.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// Address the reply came from. Null when nothing responded. Default: null.
    /// </summary>
    public string? ReplyAddress { get; set; }

    /// <summary>
    /// Status of the reply, e.g. "Success" or "TimedOut". Default: empty string.
    /// </summary>
    public string ReplyStatus { get; set; } = string.Empty;

    /// <summary>
    /// Round trip of the reply, in milliseconds. Null when it timed out. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Time-to-live of the reply. Default: null.
    /// </summary>
    public int? Ttl { get; set; }
}
