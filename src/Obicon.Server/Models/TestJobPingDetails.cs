namespace Obicon.Server.Models;

/// <summary>
/// Ping section of a run's details: one row per finished ping job.
/// </summary>
public class TestJobPingDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The pinged target. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Address the target resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Time the DNS resolution took, in milliseconds. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// Address of the last replying probe. Default: null.
    /// </summary>
    public string? ReplyAddress { get; set; }

    /// <summary>
    /// Status of the last reply, e.g. "Success". Default: empty string.
    /// </summary>
    public string ReplyStatus { get; set; } = string.Empty;

    /// <summary>
    /// Average round trip of the answered probes, in milliseconds. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Time-to-live of the last reply. Default: null.
    /// </summary>
    public int? Ttl { get; set; }

    /// <summary>
    /// Wall-clock time of the whole run, in milliseconds. Default: null.
    /// </summary>
    public double? WallclockMs { get; set; }

    /// <summary>
    /// Number of probes sent. Default: 0.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Number of probes that got a reply. Default: 0.
    /// </summary>
    public int Received { get; set; }

    /// <summary>
    /// Percentage of probes lost. Default: 0.
    /// </summary>
    public double LossPercent { get; set; }

    /// <summary>
    /// Minimum round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? MinRoundtripMs { get; set; }

    /// <summary>
    /// Average round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? AvgRoundtripMs { get; set; }

    /// <summary>
    /// Maximum round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? MaxRoundtripMs { get; set; }

    /// <summary>
    /// Error that failed the run. Null on success. Default: null.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// One row per probe reply, ordered by the Ordinal column. Default: empty list.
    /// </summary>
    public List<TestJobPingReply> Replies { get; set; } = [];
}
