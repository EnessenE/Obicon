
namespace Obicon.Server.Models;

// Normalized persistence of a finished run's structured details: one table per
// test-type section, 1:1 with the job (job_id primary key), plus child tables for
// the arrays (traceroute hops and probes, ping replies, DNS records) and a shared
// certificate table. All rows cascade on job deletion. Wire and API shapes stay the
// shared TestResultDetails; these entities are only the relational storage, mapped
// by TestResultDetailsMapper.

/// <summary>
/// Traceroute section of a run's details: one row per finished traceroute job.
/// </summary>
public class TestJobTracerouteDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Address the target resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Whether the trace reached the target. Default: false.
    /// </summary>
    public bool TargetReached { get; set; }

    /// <summary>
    /// Number of hops recorded. Default: 0.
    /// </summary>
    public int HopCount { get; set; }

    /// <summary>
    /// One row per hop, ordered by the Hop column. Default: empty list.
    /// </summary>
    public List<TestJobTracerouteHop> Hops { get; set; } = [];
}

/// <summary>
/// One hop of a traceroute run.
/// </summary>
public class TestJobTracerouteHop
{
    /// <summary>
    /// Unique identifier of the hop row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the job the hop belongs to. Cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Hop number, 1-based; orders the hops.
    /// </summary>
    public int Hop { get; set; }

    /// <summary>
    /// Address of the hop. Null when nothing responded. Default: null.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Best-effort reverse lookup of the hop address. Null when unresolved or disabled. Default: null.
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Status of the hop, e.g. "TtlExpired" or "TimedOut". Default: empty string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Average round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Error that ended the trace at this hop. Null when the hop did not error. Default: null.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// One row per probe of the hop, ordered by the Ordinal column. Default: empty list.
    /// </summary>
    public List<TestJobTracerouteProbe> Probes { get; set; } = [];
}

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

/// <summary>
/// Ping section of a run's details: one row per finished ping job.
/// </summary>
public class TestJobPingDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The pinged target. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Address the target resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Time the DNS resolution took, in milliseconds. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// Address of the last replying probe. Default: null.
    /// </summary>
    public string? ReplyAddress { get; set; }

    /// <summary>
    /// Status of the last reply, e.g. "Success". Default: empty string.
    /// </summary>
    public string ReplyStatus { get; set; } = string.Empty;

    /// <summary>
    /// Average round trip of the answered probes, in milliseconds. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Time-to-live of the last reply. Default: null.
    /// </summary>
    public int? Ttl { get; set; }

    /// <summary>
    /// Wall-clock time of the whole run, in milliseconds. Default: null.
    /// </summary>
    public double? WallclockMs { get; set; }

    /// <summary>
    /// Number of probes sent. Default: 0.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Number of probes that got a reply. Default: 0.
    /// </summary>
    public int Received { get; set; }

    /// <summary>
    /// Percentage of probes lost. Default: 0.
    /// </summary>
    public double LossPercent { get; set; }

    /// <summary>
    /// Minimum round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? MinRoundtripMs { get; set; }

    /// <summary>
    /// Average round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? AvgRoundtripMs { get; set; }

    /// <summary>
    /// Maximum round trip of the answered probes, in milliseconds. Null when nothing answered. Default: null.
    /// </summary>
    public double? MaxRoundtripMs { get; set; }

    /// <summary>
    /// Error that failed the run. Null on success. Default: null.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// One row per probe reply, ordered by the Ordinal column. Default: empty list.
    /// </summary>
    public List<TestJobPingReply> Replies { get; set; } = [];
}

/// <summary>
/// One reply of a ping run.
/// </summary>
public class TestJobPingReply
{
    /// <summary>
    /// Unique identifier of the reply row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the job the reply belongs to. Cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Position of the reply within the run, 0-based; orders the replies.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// Address the reply came from. Null when nothing responded. Default: null.
    /// </summary>
    public string? ReplyAddress { get; set; }

    /// <summary>
    /// Status of the reply, e.g. "Success" or "TimedOut". Default: empty string.
    /// </summary>
    public string ReplyStatus { get; set; } = string.Empty;

    /// <summary>
    /// Round trip of the reply, in milliseconds. Null when it timed out. Default: null.
    /// </summary>
    public double? RoundtripMs { get; set; }

    /// <summary>
    /// Time-to-live of the reply. Default: null.
    /// </summary>
    public int? Ttl { get; set; }
}

/// <summary>
/// TCP section of a run's details: one row per finished TCP job.
/// </summary>
public class TestJobTcpDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The host that was connected to. Default: empty string.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// The port that was connected to. Default: 0.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Address the host resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// IP family of the connection, "IPv4" or "IPv6". Default: null.
    /// </summary>
    public string? Family { get; set; }

    /// <summary>
    /// Time the DNS resolution took, in milliseconds. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// Time the TCP connect took, in milliseconds. Default: null.
    /// </summary>
    public double? ConnectMs { get; set; }

    /// <summary>
    /// Error that failed the run. Null on success. Default: null.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// HTTP(S) section of a run's details: one row per finished HTTP(S) job.
/// </summary>
public class TestJobHttpDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The requested URL. Default: empty string.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method used, e.g. "GET". Default: empty string.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// The final URL after redirects. Default: null.
    /// </summary>
    public string? FinalUrl { get; set; }

    /// <summary>
    /// Response status code. Default: null.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Response reason phrase. Default: null.
    /// </summary>
    public string? ReasonPhrase { get; set; }

    /// <summary>
    /// Address the host resolved to. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Time the DNS phase took, in milliseconds. Null for proxied requests. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// Time the connect phase took, in milliseconds. Null for proxied requests. Default: null.
    /// </summary>
    public double? ConnectMs { get; set; }

    /// <summary>
    /// Time the TLS handshake took, in milliseconds. Null for proxied requests. Default: null.
    /// </summary>
    public double? TlsMs { get; set; }

    /// <summary>
    /// Negotiated TLS protocol, e.g. "Tls13". Default: null.
    /// </summary>
    public string? TlsProtocol { get; set; }

    /// <summary>
    /// Negotiated TLS cipher suite. Default: null.
    /// </summary>
    public string? TlsCipher { get; set; }

    /// <summary>
    /// Time to the first response byte, in milliseconds. Default: null.
    /// </summary>
    public double? TtfbMs { get; set; }

    /// <summary>
    /// Time the body transfer took, in milliseconds. Default: null.
    /// </summary>
    public double? TransferMs { get; set; }

    /// <summary>
    /// Bytes of the response body read. Default: null.
    /// </summary>
    public long? BytesRead { get; set; }

    /// <summary>
    /// Whether the body was truncated at the read limit. Default: null.
    /// </summary>
    public bool? BytesTruncated { get; set; }

    /// <summary>
    /// The proxy URL used, when the run went through a proxy. Default: null.
    /// </summary>
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// Whether the body matched the expected pattern. Null when no pattern was set. Default: null.
    /// </summary>
    public bool? BodyMatched { get; set; }

    /// <summary>
    /// Error that failed the run. Null on success. Default: null.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// DNS section of a run's details: one row per finished DNS job.
/// </summary>
public class TestJobDnsDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The queried host. Default: empty string.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// The nameservers that were queried. Default: empty array.
    /// </summary>
    public List<string> NameserversQueried { get; set; } = [];

    /// <summary>
    /// The nameserver that answered. Default: null.
    /// </summary>
    public string? AnsweringNameserver { get; set; }

    /// <summary>
    /// Round trip of the answering nameserver, in milliseconds. Default: null.
    /// </summary>
    public double? NameserverRttMs { get; set; }

    /// <summary>
    /// The address values after the IP version filter, as a native text array. Default: empty array.
    /// </summary>
    public List<string> Resolved { get; set; } = [];

    /// <summary>
    /// How the query was performed: "nameserver", "os-resolver", or "literal". Default: empty string.
    /// </summary>
    public string Via { get; set; } = string.Empty;

    /// <summary>
    /// The answering nameserver's DNS status, e.g. "NOERROR". Default: null.
    /// </summary>
    public string? ResponseStatus { get; set; }

    /// <summary>
    /// The queried record type, e.g. "A". Default: empty string.
    /// </summary>
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// The expected address when one was configured. Default: null.
    /// </summary>
    public string? ExpectedAddress { get; set; }

    /// <summary>
    /// Whether the result matched the expectation. Null when none was configured. Default: null.
    /// </summary>
    public bool? ExpectedMatched { get; set; }

    /// <summary>
    /// Error that failed the run. Null on success. Default: null.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// One row per returned record, ordered by the Ordinal column. Default: empty list.
    /// </summary>
    public List<TestJobDnsRecord> Records { get; set; } = [];
}

/// <summary>
/// One DNS record returned by a DNS run.
/// </summary>
public class TestJobDnsRecord
{
    /// <summary>
    /// Unique identifier of the record row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the job the record belongs to. Cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Position of the record within the run, 0-based; orders the records.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// The record type, e.g. "A" or "CNAME". Default: empty string.
    /// </summary>
    public string RecordType { get; set; } = string.Empty;

    /// <summary>
    /// The record value. Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The record's time to live, in seconds. -1 when unknown. Default: -1.
    /// </summary>
    public long TtlSeconds { get; set; } = -1;
}

/// <summary>
/// TLS section of a run's details: one row per finished TLS job.
/// </summary>
public class TestJobTlsDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The host that was handshaked with. Default: empty string.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// The port that was handshaked with. Default: 0.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Address the host resolved to. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// IP family of the connection, "IPv4" or "IPv6". Default: null.
    /// </summary>
    public string? Family { get; set; }

    /// <summary>
    /// Time the DNS resolution took, in milliseconds. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// Time the TCP connect took, in milliseconds. Default: null.
    /// </summary>
    public double? ConnectMs { get; set; }

    /// <summary>
    /// Time the TLS handshake took, in milliseconds. Default: null.
    /// </summary>
    public double? HandshakeMs { get; set; }

    /// <summary>
    /// Negotiated TLS protocol, e.g. "Tls13". Default: null.
    /// </summary>
    public string? Protocol { get; set; }

    /// <summary>
    /// Negotiated TLS cipher suite. Default: null.
    /// </summary>
    public string? Cipher { get; set; }

    /// <summary>
    /// Error that failed the run. Null on success. Default: null.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// The TLS certificate of a finished HTTP(S) or TLS run. One row per job that
/// reported a certificate; keyed by the job ID.
/// </summary>
public class TestJobCertificate
{
    /// <summary>
    /// ID of the job the certificate belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The certificate subject. Default: empty string.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The certificate issuer. Default: empty string.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Start of the certificate's validity window. Default: null.
    /// </summary>
    public DateTime? NotBefore { get; set; }

    /// <summary>
    /// End of the certificate's validity window. Default: null.
    /// </summary>
    public DateTime? NotAfter { get; set; }

    /// <summary>
    /// Days until the certificate expires. Default: null.
    /// </summary>
    public double? DaysRemaining { get; set; }

    /// <summary>
    /// Subject alternative names, as a native text array. Default: empty array.
    /// </summary>
    public List<string> SubjectAlternativeNames { get; set; } = [];
}
