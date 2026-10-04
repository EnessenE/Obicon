namespace Obicon.Server.Models;

/// <summary>
/// One hop of a traceroute run.
/// </summary>
public class TestJobTracerouteHop
{
    /// <summary>
    /// Unique identifier of the hop row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the job the hop belongs to. Cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Hop number, 1-based; orders the hops.
    /// </summary>
    public int Hop { get; set; }

    /// <summary>
    /// Address of the hop. Null when nothing responded. Default: null.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Best-effort reverse lookup of the hop address. Null when unresolved or disabled. Default: null.
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Status of the hop, e.g. "TtlExpired" or "TimedOut". Default: empty string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Average round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Error that ended the trace at this hop. Null when the hop did not error. Default: null.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// One row per probe of the hop, ordered by the Ordinal column. Default: empty list.
    /// </summary>
    public List<TestJobTracerouteProbe> Probes { get; set; } = [];
}
