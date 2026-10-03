using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models;

/// <summary>
/// Represents a test execution job in the queue.
/// </summary>
public class TestJob
{
    /// <summary>
    /// Unique identifier for the job. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the test this job belongs to.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// ID of the node assigned to execute this job.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Type of test this job executes. See <see cref="TestType"/> for available types.
    /// </summary>
    public TestType TestType { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Maximum execution time for this job in seconds. Default: 60.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days. Null disables the check.
    /// </summary>
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses. Null accepts any result.
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
    /// Ping: probes sent per run, each with its own round trip time. Null uses the
    /// default (4). Default: null.
    /// </summary>
    public int? PingCount { get; set; }

    /// <summary>
    /// Ping: milliseconds to wait for each probe's reply. Null uses the default
    /// (2000). Default: null.
    /// </summary>
    public int? PingTimeoutMs { get; set; }

    /// <summary>
    /// Ping: milliseconds to wait between probes. Null uses the default (0, no wait).
    /// Default: null.
    /// </summary>
    public int? PingIntervalMs { get; set; }

    /// <summary>
    /// HTTP/HTTPS: request method, "GET" or "HEAD". Null uses the default ("GET").
    /// Default: null.
    /// </summary>
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
    /// <summary>
    /// DNS: the record type to query: "A", "AAAA", "CNAME", "TXT", "MX", or "CAA".
    /// Null queries both address families. Default: null.
    /// </summary>
    public string? DnsQueryType { get; set; }
    /// <summary>
    /// Current status of the job. See <see cref="TestJobStatus"/> for available statuses. Default: Queued.
    /// </summary>
    public TestJobStatus Status { get; set; } = TestJobStatus.Queued;

    /// <summary>
    /// Timestamp when the job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the job execution started. Null if not yet started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the node acknowledged the assignment. Null if the node never responded.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Timestamp when the job was completed. Null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Result of the test execution. Null if not yet completed.
    /// </summary>
    public TestResult? Result { get; set; }

    /// <summary>
    /// Error message if the job failed. Null if successful or not yet completed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
