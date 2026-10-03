
namespace Obicon.Shared.Models.Results;

/// <summary>
/// Traceroute result: one record per hop, in order.
/// </summary>
public class TracerouteDetails
{
    /// <summary>
    /// Address the target resolved to before tracing. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Whether the target itself answered within the hop limit. Default: false.
    /// </summary>
    public bool TargetReached { get; set; }

    /// <summary>
    /// Number of hops recorded (the target's hop when reached, the limit otherwise).
    /// </summary>
    public int HopCount { get; set; }

    /// <summary>
    /// The hops in TTL order. Default: empty list.
    /// </summary>
    public List<TracerouteHop> Hops { get; set; } = [];
}

/// <summary>
/// One hop of a traceroute: the responding router, or a timeout marker.
/// </summary>
public class TracerouteHop
{
    /// <summary>
    /// Hop number, starting at 1.
    /// </summary>
    public int Hop { get; set; }

    /// <summary>
    /// Address that answered this hop. Null when nothing responded. Default: null.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Reply status, e.g. "TtlExpired", "Success", or "TimedOut". Default: empty string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Roundtrip time of the hop's probe in milliseconds. Null when nothing responded.
    /// Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Error text when the hop ended the trace abnormally (e.g. "DestinationUnreachable").
    /// Default: null.
    /// </summary>
    public string? Error { get; set; }
}
