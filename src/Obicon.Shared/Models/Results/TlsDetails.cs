namespace Obicon.Shared.Models.Results;

/// <summary>
/// TLS handshake result: the negotiated parameters plus the server's certificate.
/// </summary>
public class TlsDetails
{
    /// <summary>
    /// Host the test connected to, without the port. Default: empty string.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Port the test connected to (default 443 when the target had none).
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Address the host resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Address family of the resolved address, e.g. "InterNetworkV6". Default: null.
    /// </summary>
    public string? Family { get; set; }

    /// <summary>
    /// DNS resolution time in milliseconds. Null when resolution failed. Default: null.
    /// </summary>
    public double? DnsMs { get; set; }

    /// <summary>
    /// TCP connect time in milliseconds. Null when the connection failed.
    /// Default: null.
    /// </summary>
    public double? ConnectMs { get; set; }

    /// <summary>
    /// TLS handshake time in milliseconds. Null when the handshake failed.
    /// Default: null.
    /// </summary>
    public double? HandshakeMs { get; set; }

    /// <summary>
    /// Negotiated TLS protocol, e.g. "Tls13". Null when the handshake failed.
    /// Default: null.
    /// </summary>
    public string? Protocol { get; set; }

    /// <summary>
    /// Negotiated TLS cipher suite, e.g. "Tls13Aes128GcmSha256". Null when the
    /// handshake failed. Default: null.
    /// </summary>
    public string? Cipher { get; set; }

    /// <summary>
    /// The server's certificate. Null when the handshake failed. Default: null.
    /// </summary>
    public CertificateDetails? Certificate { get; set; }

    /// <summary>
    /// Error text when resolution, the connection, or the handshake failed. Default: null.
    /// </summary>
    public string? Error { get; set; }
}
