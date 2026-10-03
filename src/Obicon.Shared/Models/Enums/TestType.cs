namespace Obicon.Shared.Models.Enums;

/// <summary>
/// Types of synthetic tests that can be executed by nodes.
/// </summary>
public enum TestType
{
    /// <summary>
    /// ICMP ping test to check connectivity.
    /// </summary>
    Ping,

    /// <summary>
    /// Traceroute test to trace network path.
    /// </summary>
    Traceroute,

    /// <summary>
    /// HTTP test for unencrypted endpoints.
    /// </summary>
    Http,

    /// <summary>
    /// HTTPS test for encrypted endpoints.
    /// </summary>
    Https,

    /// <summary>
    /// TCP connection test to verify port accessibility.
    /// </summary>
    Tcp,

    /// <summary>
    /// DNS resolution test to verify domain resolution.
    /// </summary>
    Dns,

    /// <summary>
    /// TLS handshake test reporting the certificate and negotiated parameters.
    /// </summary>
    Tls
}
