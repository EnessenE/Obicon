namespace Obicon.Shared.Models.Results;

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
    /// Hostname the address resolved to (best effort, null when unresolved or
    /// disabled). Default: null.
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Reply status, e.g. "TtlExpired", "Success", or "TimedOut". Default: empty string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Average roundtrip time of the hop's probes that got a reply, in milliseconds.
    /// Null when nothing responded. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// One record per probe sent to this hop, in order. Default: empty list.
    /// </summary>
    public List<TracerouteProbe> Probes { get; set; } = [];

    /// <summary>
    /// Error text when the hop ended the trace abnormally (e.g. "DestinationUnreachable").
    /// Default: null.
    /// </summary>
    public string? Error { get; set; }
}
