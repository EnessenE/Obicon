namespace Obicon.Server.Models;

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
