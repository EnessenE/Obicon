
namespace Obicon.Shared.Models.Results;

/// <summary>
/// Structured result details of a test run, sent with the TestResult message and
/// stored on the job. Exactly one section is populated, matching the executed
/// test type; HTTP and HTTPS runs both use the Http section. Default: all null.
/// </summary>
public class TestResultDetails
{
    /// <summary>
    /// Traceroute details: one record per hop. Default: null.
    /// </summary>
    public TracerouteDetails? Traceroute { get; set; }

    /// <summary>
    /// Ping details: resolution, reply status, roundtrip, TTL. Default: null.
    /// </summary>
    public PingDetails? Ping { get; set; }

    /// <summary>
    /// TCP connect details: resolution and connect timing. Default: null.
    /// </summary>
    public TcpDetails? Tcp { get; set; }

    /// <summary>
    /// HTTP/HTTPS details: per-phase timings, status, certificate, checks.
    /// Default: null.
    /// </summary>
    public HttpDetails? Http { get; set; }

    /// <summary>
    /// DNS details: nameservers queried, records returned, expectation match.
    /// Default: null.
    /// </summary>
    public DnsDetails? Dns { get; set; }

    /// <summary>
    /// TLS handshake details: negotiated protocol and cipher plus the server's
    /// certificate. Default: null.
    /// </summary>
    public TlsDetails? Tls { get; set; }
}
