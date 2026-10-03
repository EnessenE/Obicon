using System.Text.Json.Serialization;
using Obicon.Shared.Models.Enums;

namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent by the server to assign a single test run to a node.
/// </summary>
public class TestAssignmentMessage
{
    /// <summary>
    /// Unique identifier of the test job. Echoed back in result and status messages.
    /// </summary>
    [JsonPropertyName("JobId")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the test to execute.
    /// </summary>
    [JsonPropertyName("TestId")]
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types.
    /// </summary>
    [JsonPropertyName("TestType")]
    public TestType TestType { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest.
    /// </summary>
    [JsonPropertyName("Target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Maximum execution time for this test in seconds. Default: 60. Capped at 60.
    /// </summary>
    [JsonPropertyName("TimeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    [JsonPropertyName("ExpectedStatusCodes")]
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days.
    /// Null disables the expiry check. Default: null.
    /// </summary>
    [JsonPropertyName("CheckCertificateExpiryDays")]
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses.
    /// Null accepts any successfully resolved result. Default: null.
    /// </summary>
    [JsonPropertyName("ExpectedDnsResult")]
    public string? ExpectedDnsResult { get; set; }

    /// <summary>
    /// IP version the test should use. Default: Any.
    /// </summary>
    [JsonPropertyName("IpVersion")]
    public IpVersion IpVersion { get; set; } = IpVersion.Any;

    /// <summary>
    /// HTTP/HTTPS: when set, the response body must match this regular expression
    /// or the run fails. Null disables the check. Default: null.
    /// </summary>
    [JsonPropertyName("ExpectedBodyPattern")]
    public string? ExpectedBodyPattern { get; set; }

    /// <summary>
    /// HTTP/HTTPS: custom headers sent with the request, e.g. authentication headers.
    /// Default: empty dictionary.
    /// </summary>
    [JsonPropertyName("Headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// HTTP/HTTPS: URL of an HTTP proxy the request goes through, e.g. "http://proxy:8080".
    /// Null connects directly. Default: null.
    /// </summary>
    [JsonPropertyName("ProxyUrl")]
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// HTTP/HTTPS: when true, a unique query parameter is appended to the request URL
    /// so caches serve a fresh response. Default: false.
    /// </summary>
    [JsonPropertyName("CacheBust")]
    public bool CacheBust { get; set; }

    /// <summary>
    /// Traceroute: maximum number of hops before giving up. Null uses the default (30).
    /// Default: null.
    /// </summary>
    [JsonPropertyName("TracerouteMaxHops")]
    public int? TracerouteMaxHops { get; set; }

    /// <summary>
    /// Traceroute: probes sent per hop, each with its own round trip time. Null uses
    /// the default (3). Default: null.
    /// </summary>
    [JsonPropertyName("TracerouteQueriesPerHop")]
    public int? TracerouteQueriesPerHop { get; set; }

    /// <summary>
    /// Traceroute: milliseconds to wait for each probe's reply. Null uses the
    /// default (2000). Default: null.
    /// </summary>
    [JsonPropertyName("TracerouteQueryTimeoutMs")]
    public int? TracerouteQueryTimeoutMs { get; set; }

    /// <summary>
    /// Traceroute: when true, each hop's address is resolved to a hostname
    /// (best effort). Null uses the default (true). Default: null.
    /// </summary>
    [JsonPropertyName("TracerouteResolveHostnames")]
    public bool? TracerouteResolveHostnames { get; set; }

    /// <summary>
    /// Ping: probes sent per run, each with its own round trip time. Null uses the
    /// default (4). Default: null.
    /// </summary>
    [JsonPropertyName("PingCount")]
    public int? PingCount { get; set; }

    /// <summary>
    /// Ping: milliseconds to wait for each probe's reply. Null uses the default
    /// (2000). Default: null.
    /// </summary>
    [JsonPropertyName("PingTimeoutMs")]
    public int? PingTimeoutMs { get; set; }

    /// <summary>
    /// Ping: milliseconds to wait between probes. Null uses the default (0, no wait).
    /// Default: null.
    /// </summary>
    [JsonPropertyName("PingIntervalMs")]
    public int? PingIntervalMs { get; set; }

    /// <summary>
    /// HTTP/HTTPS: request method, "GET" or "HEAD". Null uses the default ("GET").
    /// Default: null.
    /// </summary>
    [JsonPropertyName("HttpMethod")]
    public string? HttpMethod { get; set; }

    /// <summary>
    /// HTTP/HTTPS: when true, redirects are followed up to the handler's limit. Null
    /// uses the default (true). Default: null.
    /// </summary>
    [JsonPropertyName("FollowRedirects")]
    public bool? FollowRedirects { get; set; }

    /// <summary>
    /// DNS: address of the nameserver to query instead of the system's, e.g.
    /// "8.8.8.8". Null uses the system nameservers. Default: null.
    /// </summary>
    [JsonPropertyName("DnsNameserver")]
    public string? DnsNameserver { get; set; }

    /// <summary>
    /// DNS: the record type to query: "A", "AAAA", "CNAME", "TXT", "MX", or "CAA".
    /// Null queries both address families. Default: null.
    /// </summary>
    [JsonPropertyName("DnsQueryType")]
    public string? DnsQueryType { get; set; }
}
