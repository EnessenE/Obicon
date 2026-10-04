
namespace Obicon.Shared.Models.Results;

/// <summary>
/// HTTP/HTTPS result: per-phase timings, response status, TLS details, and the
/// outcomes of the configured checks.
/// </summary>
public class HttpDetails
{
    /// <summary>
    /// Request URL after scheme defaulting and cache busting. Default: empty string.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Request method used, "GET" or "HEAD". Default: empty string.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Final URL after redirects. Default: null.
    /// </summary>
    public string? FinalUrl { get; set; }

    /// <summary>
    /// HTTP status code of the response. Null when the request failed. Default: null.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Reason phrase of the response, e.g. "OK". Default: null.
    /// </summary>
    public string? ReasonPhrase { get; set; }

    /// <summary>
    /// Address the host resolved to. Null for proxied requests or failed
    /// resolution. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// DNS resolution time in milliseconds. Null for proxied requests. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// TCP connect time in milliseconds. Null for proxied requests. Default: null.
    /// </summary>
    public double? ConnectMs { get; set; }

    /// <summary>
    /// TLS handshake time in milliseconds. Null for plain HTTP or proxied requests.
    /// Default: null.
    /// </summary>
    public double? TlsMs { get; set; }

    /// <summary>
    /// Negotiated TLS protocol, e.g. "Tls13". Null for plain HTTP. Default: null.
    /// </summary>
    public string? TlsProtocol { get; set; }

    /// <summary>
    /// Negotiated TLS cipher suite, e.g. "Tls13Aes128GcmSha256". Null for plain HTTP.
    /// Default: null.
    /// </summary>
    public string? TlsCipher { get; set; }

    /// <summary>
    /// Time to first byte in milliseconds: status line plus headers. Null when the
    /// request failed. Default: null.
    /// </summary>
    public double? TtfbMs { get; set; }

    /// <summary>
    /// Response body transfer time in milliseconds. Null when the request failed.
    /// Default: null.
    /// </summary>
    public double? TransferMs { get; set; }

    /// <summary>
    /// Response body bytes read. Null when the request failed. Default: null.
    /// </summary>
    public long? BytesRead { get; set; }

    /// <summary>
    /// Whether the body was truncated at the 2 MB read cap. Null when the request
    /// failed. Default: null.
    /// </summary>
    public bool? BytesTruncated { get; set; }

    /// <summary>
    /// Proxy URL the request went through. Null for direct requests. Default: null.
    /// </summary>
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// The server's TLS certificate. Null for plain HTTP. Default: null.
    /// </summary>
    public CertificateDetails? Certificate { get; set; }

    /// <summary>
    /// Whether the response body matched ExpectedBodyPattern. Null when no pattern
    /// was set. Default: null.
    /// </summary>
    public bool? BodyMatched { get; set; }

    /// <summary>
    /// Error text when the request failed, e.g. the socket error code. Default: null.
    /// </summary>
    public string? Error { get; set; }
}
