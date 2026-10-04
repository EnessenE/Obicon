
namespace Obicon.Shared.Models.Results;

/// <summary>
/// DNS resolution result: which nameservers were asked, which one answered,
/// and the records it returned.
/// </summary>
public class DnsDetails
{
    /// <summary>
    /// Hostname the test resolved. Default: empty string.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// System nameservers that were queried. Empty for the OS-resolver fallback.
    /// Default: empty list.
    /// </summary>
    public List<string> NameserversQueried { get; set; } = [];

    /// <summary>
    /// Nameserver that answered first. Null for the OS-resolver fallback.
    /// Default: null.
    /// </summary>
    public string? AnsweringNameserver { get; set; }

    /// <summary>
    /// Roundtrip time of the answering nameserver in milliseconds. Null for the
    /// OS-resolver fallback. Default: null.
    /// </summary>
    public double? NameserverRttMs { get; set; }

    /// <summary>
    /// Records the answer contained, of every queried type. Default: empty list.
    /// </summary>
    public List<DnsRecord> Records { get; set; } = [];

    /// <summary>
    /// Record values left after applying the test's IP version filter (address
    /// records only; other types pass through unfiltered). Default: empty list.
    /// </summary>
    public List<string> Resolved { get; set; } = [];

    /// <summary>
    /// How the answer was obtained: "nameserver" or "os-resolver". Default: empty string.
    /// </summary>
    public string Via { get; set; } = string.Empty;

    /// <summary>
    /// DNS response status of the answering nameserver, e.g. "NOERROR" or "NXDOMAIN".
    /// Null for the OS-resolver fallback. Default: null.
    /// </summary>
    public string? ResponseStatus { get; set; }

    /// <summary>
    /// The record type the test queried: "A", "AAAA", "CNAME", "TXT", "MX", "CAA",
    /// or "Any". Default: empty string.
    /// </summary>
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// Address the test expected among the resolved ones. Null when no expectation
    /// was set. Default: null.
    /// </summary>
    public string? ExpectedAddress { get; set; }

    /// <summary>
    /// Whether the expected address was among the resolved ones. Null when no
    /// expectation was set. Default: null.
    /// </summary>
    public bool? ExpectedMatched { get; set; }

    /// <summary>
    /// Error text when resolution failed, e.g. the socket error code. Default: null.
    /// </summary>
    public string? Error { get; set; }
}
