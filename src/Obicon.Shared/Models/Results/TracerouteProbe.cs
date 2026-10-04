namespace Obicon.Shared.Models.Results;

/// <summary>
/// One traceroute probe: a single ping sent with the hop's TTL.
/// </summary>
public class TracerouteProbe
{
    /// <summary>
    /// Reply status, e.g. "TtlExpired", "Success", or "TimedOut". Default: empty string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Roundtrip time of this probe in milliseconds. Null when nothing responded.
    /// Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }
}
