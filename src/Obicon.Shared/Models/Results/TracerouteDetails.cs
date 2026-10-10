
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
