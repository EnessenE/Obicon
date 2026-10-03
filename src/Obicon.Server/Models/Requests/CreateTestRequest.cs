using System.ComponentModel.DataAnnotations;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Requests;

public class CreateTestRequest
{
    /// <summary>
    /// Human-readable name of the test. Required, at least 1 character.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types. Required.
    /// </summary>
    [EnumDataType(typeof(TestType))]
    public TestType Type { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Required.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// List of node IDs that should execute this test. At least one node or pool is required. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// List of pool IDs this test targets; all member nodes of these pools execute it too. Default: empty list.
    /// </summary>
    public List<Guid> PoolIds { get; set; } = new();

    /// <summary>
    /// How often the test should be executed, in seconds. Must be one of the FrequencyPresetsSeconds server setting values.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Frequency { get; set; }

    /// <summary>
    /// Indicates if the test should be active immediately. Default: true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    [RegularExpression(@"^\d{3}(-\d{3})?(,\d{3}(-\d{3})?)*$", ErrorMessage = "Use status codes like 200-399 or 200,301,302")]
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days.
    /// Null disables the expiry check. Default: null.
    /// </summary>
    [Range(0, 3650)]
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses.
    /// Null accepts any successfully resolved result. Default: null.
    /// </summary>
    public string? ExpectedDnsResult { get; set; }

    /// <summary>
    /// IP version the test should use. Default: Any.
    /// </summary>
    [EnumDataType(typeof(IpVersion))]
    public IpVersion IpVersion { get; set; } = IpVersion.Any;

    /// <summary>
    /// Maximum execution time per run in seconds, between 1 and the server's MaxTestTimeoutSeconds. Default: 60.
    /// </summary>
    [Range(1, 3600)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// HTTP/HTTPS: when set, the response body must match this regular expression
    /// or the run fails. Null disables the check. Default: null.
    /// </summary>
    [MaxLength(2000)]
    public string? ExpectedBodyPattern { get; set; }

    /// <summary>
    /// HTTP/HTTPS: custom headers sent with the request, e.g. authentication headers. Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// HTTP/HTTPS: absolute http:// or https:// URL of a proxy the request goes through.
    /// Null connects directly. Default: null.
    /// </summary>
    [MaxLength(500)]
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// HTTP/HTTPS: when true, a unique query parameter is appended to the request URL
    /// so caches serve a fresh response. Default: false.
    /// </summary>
    public bool CacheBust { get; set; }

    /// <summary>
    /// Traceroute: maximum number of hops before giving up. Null uses the default (30).
    /// Range: 1-64. Default: null.
    /// </summary>
    [Range(1, 64)]
    public int? TracerouteMaxHops { get; set; }

    /// <summary>
    /// Traceroute: probes sent per hop, each with its own round trip time. Null uses
    /// the default (3). Range: 1-10. Default: null.
    /// </summary>
    [Range(1, 10)]
    public int? TracerouteQueriesPerHop { get; set; }

    /// <summary>
    /// Traceroute: milliseconds to wait for each probe's reply. Null uses the default
    /// (2000). Range: 100-60000. Default: null.
    /// </summary>
    [Range(100, 60_000)]
    public int? TracerouteQueryTimeoutMs { get; set; }

    /// <summary>
    /// Traceroute: when true, each hop's address is resolved to a hostname (best
    /// effort). Null uses the default (true). Default: null.
    /// </summary>
    public bool? TracerouteResolveHostnames { get; set; }
    /// <summary>
    /// Ping: probes sent per run, each with its own round trip time. Null uses the
    /// default (4). Range: 1-100. Default: null.
    /// </summary>
    [Range(1, 100)]
    public int? PingCount { get; set; }

    /// <summary>
    /// Ping: milliseconds to wait for each probe's reply. Null uses the default (2000).
    /// Range: 100-60000. Default: null.
    /// </summary>
    [Range(100, 60_000)]
    public int? PingTimeoutMs { get; set; }

    /// <summary>
    /// Ping: milliseconds to wait between probes. Null uses the default (0, no wait).
    /// Range: 0-10000. Default: null.
    /// </summary>
    [Range(0, 10_000)]
    public int? PingIntervalMs { get; set; }

    /// <summary>
    /// HTTP/HTTPS: request method, "GET" or "HEAD". Null uses the default ("GET").
    /// Default: null.
    /// </summary>
    [RegularExpression("^(GET|HEAD)$", ErrorMessage = "The HttpMethod field must be GET or HEAD.")]
    public string? HttpMethod { get; set; }

    /// <summary>
    /// HTTP/HTTPS: when true, redirects are followed up to the handler's limit. Null
    /// uses the default (true). Default: null.
    /// </summary>
    public bool? FollowRedirects { get; set; }

    /// <summary>
    /// DNS: address of the nameserver to query instead of the system's, e.g. "8.8.8.8".
    /// Null uses the system nameservers. Default: null.
    /// </summary>
    public string? DnsNameserver { get; set; }
}
