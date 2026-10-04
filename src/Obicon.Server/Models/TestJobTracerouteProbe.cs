namespace Obicon.Server.Models;

/// <summary>
/// One probe of a traceroute hop.
/// </summary>
public class TestJobTracerouteProbe
{
    /// <summary>
    /// ID of the hop this probe belongs to. Primary key, cascades on hop deletion.
    /// </summary>
    public Guid HopId { get; set; }

    /// <summary>
    /// Position of the probe within its hop, 0-based; orders the probes.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// Status of the probe, e.g. "TimedOut". Default: empty string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Round trip of the probe in milliseconds. Null when it did not answer. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }
}
