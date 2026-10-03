
namespace Obicon.Shared.Models.Results;

/// <summary>
/// Ping result: resolution, the single reply's status, and its roundtrip time.
/// </summary>
public class PingDetails
{
    /// <summary>
    /// Target the test pinged. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Address the target resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// DNS resolution time in milliseconds. Null when resolution failed. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// Address the reply came from. Null when none was received. Default: null.
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
    /// Time to live of the reply packet. Null when the platform or reply
    /// did not report it. Default: null.
    /// </summary>
    public int? Ttl { get; set; }

    /// <summary>
    /// Total wall-clock time of the ping operation in milliseconds, including
    /// resolution. Default: null.
    /// </summary>
    public double? WallclockMs { get; set; }

    /// <summary>
    /// Error text when resolution failed, e.g. the socket error code. Default: null.
    /// </summary>
    public string? Error { get; set; }
}
