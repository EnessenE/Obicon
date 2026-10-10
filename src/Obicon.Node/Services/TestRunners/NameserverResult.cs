using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Result of querying one nameserver.
/// </summary>
public class NameserverResult
{
    /// <summary>
    /// Nameserver that was queried.
    /// </summary>
    public string Nameserver { get; init; } = string.Empty;

    /// <summary>
    /// Round-trip time of the queries in milliseconds. -1 when the query failed.
    /// </summary>
    public double RttMs { get; init; } = -1;

    /// <summary>
    /// Records of every queried type the answer contained. Default: empty list.
    /// </summary>
    public List<DnsRecord> Records { get; init; } = [];

    /// <summary>
    /// Response status per queried type, e.g. "A" => "NOERROR". Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string> Statuses { get; init; } = [];

    /// <summary>
    /// Error description when the query failed. Null on success.
    /// </summary>
    public string? Error { get; init; }
}
