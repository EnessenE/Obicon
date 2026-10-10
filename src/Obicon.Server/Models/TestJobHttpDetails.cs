namespace Obicon.Server.Models;

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
