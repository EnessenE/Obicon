namespace Obicon.Shared.Models.Results;

/// <summary>
/// One ping probe's reply.
/// </summary>
public class PingReply
{
    /// <summary>
    /// Address the reply came from. Null when nothing was received. Default: null.
    /// </summary>
    public string? ReplyAddress { get; set; }

    /// <summary>
    /// Reply status, e.g. "Success" or "TimedOut". Default: empty string.
    /// </summary>
    public string ReplyStatus { get; set; } = string.Empty;

    /// <summary>
    /// Roundtrip time in milliseconds. Null when nothing was received. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Time to live of the reply packet. Null when not reported. Default: null.
    /// </summary>
    public int? Ttl { get; set; }
}
