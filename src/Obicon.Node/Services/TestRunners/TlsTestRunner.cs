using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// TLS handshake test. Connects to host:port, performs the handshake, and reports
/// the negotiated protocol and cipher plus the server's certificate; optionally
/// fails when the certificate expires within the configured threshold.
/// </summary>
public partial class TlsTestRunner : ITestRunner
{
    private const int DefaultPort = 443;

    private readonly ILogger<TlsTestRunner> _logger;

    public TlsTestRunner(ILogger<TlsTestRunner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TestType Type => TestType.Tls;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var separator = assignment.Target.LastIndexOf(':');
        var host = separator > 0 ? assignment.Target[..separator] : assignment.Target;
        var port = separator > 0 && int.TryParse(assignment.Target[(separator + 1)..], out var parsed) ? parsed : DefaultPort;

        var details = new TlsDetails { Host = host, Port = port };

        IPAddress? address;
        try
        {
            var dnsStopwatch = Stopwatch.StartNew();
            address = await HostResolver.ResolveAsync(host, assignment.IpVersion, cancellationToken);
            dnsStopwatch.Stop();
            details.DnsMs = Math.Round(dnsStopwatch.Elapsed.TotalMilliseconds, 2);

            if (address == null)
            {
                LogNoAddress(host, HostResolver.FamilyName(assignment.IpVersion));
                details.Error = $"no {HostResolver.FamilyName(assignment.IpVersion)} address";
                return new TestOutcome
                {
                    Success = false,
                    Output = $"{host} has no {HostResolver.FamilyName(assignment.IpVersion)} address",
                    Details = new TestResultDetails { Tls = details }
                };
            }

            details.ResolvedAddress = address.ToString();
            details.Family = address.AddressFamily.ToString();
            LogResolved(host, address, dnsStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException ex)
        {
            details.Error = ex.SocketErrorCode.ToString();
            return new TestOutcome
            {
                Success = false,
                Output = $"Could not resolve {host}: {ex.SocketErrorCode}",
                Details = new TestResultDetails { Tls = details }
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        X509Certificate2? serverCertificate = null;
        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        double connectMs;
        try
        {
            var connectStopwatch = Stopwatch.StartNew();
            await socket.ConnectAsync(new IPEndPoint(address, port), timeoutCts.Token);
            connectStopwatch.Stop();
            connectMs = connectStopwatch.Elapsed.TotalMilliseconds;
            details.ConnectMs = Math.Round(connectMs, 2);
            LogTlsConnected(host, port, connectMs);
        }
        catch (SocketException ex)
        {
            LogTlsConnectFailed(host, port, ex.SocketErrorCode);
            details.Error = ex.SocketErrorCode.ToString();
            return new TestOutcome
            {
                Success = false,
                Output = $"TLS connect to {host}:{port} failed: {ex.SocketErrorCode}",
                Details = new TestResultDetails { Tls = details }
            };
        }
        catch (OperationCanceledException)
        {
            details.Error = "connect timed out";
            return new TestOutcome
            {
                Success = false,
                Output = $"TLS connect to {host}:{port} timed out",
                Details = new TestResultDetails { Tls = details }
            };
        }

        using var networkStream = new NetworkStream(socket, ownsSocket: true);
        using var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
        try
        {
            var handshakeStopwatch = Stopwatch.StartNew();
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                RemoteCertificateValidationCallback = (_, cert, _, _) =>
                {
                    serverCertificate = (X509Certificate2?)cert;
                    return true;
                }
            }, timeoutCts.Token);
            handshakeStopwatch.Stop();

            details.HandshakeMs = Math.Round(handshakeStopwatch.Elapsed.TotalMilliseconds, 2);
            details.Protocol = sslStream.SslProtocol.ToString();
            details.Cipher = sslStream.NegotiatedCipherSuite.ToString();
            LogTlsHandshake(host, port, handshakeStopwatch.Elapsed.TotalMilliseconds, sslStream.SslProtocol, sslStream.NegotiatedCipherSuite);

            if (serverCertificate == null)
            {
                details.Error = "the server did not present a certificate";
                return new TestOutcome
                {
                    Success = false,
                    Output = $"TLS handshake with {host}:{port} succeeded but no certificate was presented",
                    Details = new TestResultDetails { Tls = details }
                };
            }

            var daysRemaining = (serverCertificate.NotAfter - DateTime.UtcNow).TotalDays;
            details.Certificate = CertificateInfo.ToDetails(serverCertificate);

            if (assignment.CheckCertificateExpiryDays is int threshold && daysRemaining < threshold)
            {
                details.Error = $"certificate expires within the {threshold}-day threshold";
                return new TestOutcome
                {
                    Success = false,
                    Output = $"TLS handshake with {host}:{port} succeeded, but the certificate expires in {daysRemaining:F0} days (threshold {threshold})",
                    Details = new TestResultDetails { Tls = details }
                };
            }

            return new TestOutcome
            {
                Success = true,
                Output = $"TLS handshake with {host}:{port} succeeded: {sslStream.SslProtocol}, {sslStream.NegotiatedCipherSuite}, certificate expires {serverCertificate.NotAfter:yyyy-MM-dd} ({daysRemaining:F0} days)",
                Details = new TestResultDetails { Tls = details }
            };
        }
        catch (AuthenticationException ex)
        {
            LogTlsHandshakeFailed(host, port, ex.Message);
            details.Error = ex.Message;
            return new TestOutcome
            {
                Success = false,
                Output = $"TLS handshake with {host}:{port} failed: {ex.Message}",
                Details = new TestResultDetails { Tls = details }
            };
        }
        catch (OperationCanceledException)
        {
            details.Error = "handshake timed out";
            return new TestOutcome
            {
                Success = false,
                Output = $"TLS handshake with {host}:{port} timed out",
                Details = new TestResultDetails { Tls = details }
            };
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Host} has no {Family} address")]
    private partial void LogNoAddress(string host, string family);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Host} to {Address} in {DnsMs:F1} ms")]
    private partial void LogResolved(string host, IPAddress address, double dnsMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "TLS connected to {Host}:{Port} in {ConnectMs:F1} ms")]
    private partial void LogTlsConnected(string host, int port, double connectMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TLS connect to {Host}:{Port} failed: {Error}")]
    private partial void LogTlsConnectFailed(string host, int port, SocketError error);

    [LoggerMessage(Level = LogLevel.Information, Message = "TLS handshake with {Host}:{Port} finished in {HandshakeMs:F1} ms ({Protocol}, {Cipher})")]
    private partial void LogTlsHandshake(string host, int port, double handshakeMs, System.Security.Authentication.SslProtocols protocol, System.Net.Security.TlsCipherSuite cipher);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TLS handshake with {Host}:{Port} failed: {Error}")]
    private partial void LogTlsHandshakeFailed(string host, int port, string error);
}
