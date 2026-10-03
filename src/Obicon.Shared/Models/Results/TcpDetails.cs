
namespace Obicon.Shared.Models.Results;

/// <summary>
/// TCP connect result: resolution and connection timing.
/// </summary>
public class TcpDetails
{
    /// <summary>
    /// Host the test connected to, without the port. Default: empty string.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Port the test connected to (default 80 when the target had none).
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Address the host resolved to. Null when resolution failed. Default: null.
    /// </summary>
    public string? ResolvedAddress { get; set; }

    /// <summary>
    /// Address family of the resolved address, e.g. "InterNetworkV6".
    /// Default: null.
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
    /// Error text when resolution or the connection failed, e.g. the socket
    /// error code. Default: null.
    /// </summary>
    public string? Error { get; set; }
}
