using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models;

public class Test
{
    /// <summary>
    /// Unique identifier for the test. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the test. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types.
    /// </summary>
    public TestType Type { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// List of node IDs that should execute this test. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// List of pool IDs this test targets; all member nodes of these pools execute it too. Default: empty list.
    /// </summary>
    public List<Guid> PoolIds { get; set; } = new();

    /// <summary>
    /// How often the test should be executed, in seconds. Must be one of the FrequencyPresetsSeconds server setting values.
    /// </summary>
    public int Frequency { get; set; }

    /// <summary>
    /// Indicates if the test is currently active and should be scheduled. Default: true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days.
    /// Null disables the expiry check. Default: null.
    /// </summary>
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses.
    /// Null accepts any successfully resolved result. Default: null.
    /// </summary>
    public string? ExpectedDnsResult { get; set; }

    /// <summary>
    /// IP version the test should use. Default: Any.
    /// </summary>
    public IpVersion IpVersion { get; set; } = IpVersion.Any;

    /// <summary>
    /// HTTP/HTTPS: when set, the response body must match this regular expression
    /// or the run fails. Null disables the check. Default: null.
    /// </summary>
    public string? ExpectedBodyPattern { get; set; }

    /// <summary>
    /// HTTP/HTTPS: custom headers sent with the request. Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// HTTP/HTTPS: URL of an HTTP proxy the request goes through. Null connects directly. Default: null.
    /// </summary>
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// HTTP/HTTPS: when true, a unique query parameter is appended to the request URL
    /// so caches serve a fresh response. Default: false.
    /// </summary>
    public bool CacheBust { get; set; }

    /// <summary>
    /// Traceroute: maximum number of hops before giving up. Null uses the default (30).
    /// Default: null.
    /// </summary>
    public int? TracerouteMaxHops { get; set; }

    /// <summary>
    /// Traceroute: probes sent per hop, each with its own round trip time. Null uses
    /// the default (3). Default: null.
    /// </summary>
    public int? TracerouteQueriesPerHop { get; set; }

    /// <summary>
    /// Traceroute: milliseconds to wait for each probe's reply. Null uses the default
    /// (2000). Default: null.
    /// </summary>
    public int? TracerouteQueryTimeoutMs { get; set; }

    /// <summary>
    /// Traceroute: when true, each hop's address is resolved to a hostname (best
    /// effort). Null uses the default (true). Default: null.
    /// </summary>
    public bool? TracerouteResolveHostnames { get; set; }
    /// <summary>
    /// Maximum execution time per run in seconds, between 1 and the server's MaxTestTimeoutSeconds. Default: 60.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Timestamp when the scheduler last enqueued this test. Null if never scheduled.
    /// </summary>
    public DateTime? LastScheduledAt { get; set; }

    /// <summary>
    /// Timestamp when the test was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last update to the test. Null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
