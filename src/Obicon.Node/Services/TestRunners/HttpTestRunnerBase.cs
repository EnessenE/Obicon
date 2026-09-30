using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Shared logic for HTTP and HTTPS tests. Owns the connection so every phase is timed:
/// DNS resolution, TCP connect, TLS handshake, time to first byte, and transfer.
/// Also matches expected status codes and checks TLS certificate expiry.
/// </summary>
public abstract class HttpTestRunnerBase : ITestRunner
{
    private const int MaxBodyBytes = 2 * 1024 * 1024;

    /// <inheritdoc />
    protected abstract string Scheme { get; }

    /// <inheritdoc />
    public abstract TestType Type { get; }

    private readonly ILogger _logger;

    protected HttpTestRunnerBase(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var target = assignment.Target;
        var url = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? target
            : $"{Scheme}{target}";

        var metrics = new Dictionary<string, object> { ["url"] = url };
        var host = new Uri(url).Host;

        X509Certificate2? serverCertificate = null;
        double dnsMs = -1, connectMs = -1, tlsMs = -1;

        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            ConnectCallback = async (context, ct) =>
            {
                var dnsStopwatch = Stopwatch.StartNew();
                var resolvedAddress = await HostResolver.ResolveAsync(context.DnsEndPoint.Host, assignment.IpVersion, ct)
                    ?? throw new SocketException((int)SocketError.HostNotFound);
                dnsStopwatch.Stop();
                dnsMs = dnsStopwatch.Elapsed.TotalMilliseconds;
                _logger.LogInformation("DNS resolved {Host} to {Address} in {DnsMs:F1} ms", context.DnsEndPoint.Host, resolvedAddress, dnsMs);
                metrics["dns_resolved"] = resolvedAddress.ToString();
                metrics["dns_ms"] = Math.Round(dnsMs, 2);

                var socket = new Socket(resolvedAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                var connectStopwatch = Stopwatch.StartNew();
                await socket.ConnectAsync(new IPEndPoint(resolvedAddress, context.DnsEndPoint.Port), ct);
                connectStopwatch.Stop();
                connectMs = connectStopwatch.Elapsed.TotalMilliseconds;
                _logger.LogInformation("TCP connected to {Host}:{Port} in {ConnectMs:F1} ms", context.DnsEndPoint.Host, context.DnsEndPoint.Port, connectMs);
                metrics["connect_ms"] = Math.Round(connectMs, 2);

                var networkStream = new NetworkStream(socket, ownsSocket: true);

                if (!context.InitialRequestMessage.RequestUri!.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    return networkStream;
                }

                var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);

                var tlsStopwatch = Stopwatch.StartNew();
                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = context.DnsEndPoint.Host,
                    RemoteCertificateValidationCallback = (_, cert, _, _) =>
                    {
                        serverCertificate = (X509Certificate2?)cert;
                        return true;
                    }
                }, ct);
                tlsStopwatch.Stop();
                tlsMs = tlsStopwatch.Elapsed.TotalMilliseconds;
                _logger.LogInformation("TLS handshake with {Host} finished in {TlsMs:F1} ms ({Protocol}, {Cipher})",
                    context.DnsEndPoint.Host, tlsMs, sslStream.SslProtocol, sslStream.CipherAlgorithm);
                metrics["tls_ms"] = Math.Round(tlsMs, 2);
                metrics["tls_protocol"] = sslStream.SslProtocol.ToString();
                metrics["tls_cipher"] = sslStream.CipherAlgorithm.ToString();

                return sslStream;
            }
        };
        using var client = new HttpClient(handler)
        {
            // Timeout is enforced per test via the cancellation token
            Timeout = Timeout.InfiniteTimeSpan
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var totalStopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var statusCode = (int)response.StatusCode;
            var ttfbMs = totalStopwatch.Elapsed.TotalMilliseconds;
            metrics["ttfb_ms"] = Math.Round(ttfbMs, 2);
            metrics["status_code"] = statusCode;
            metrics["final_url"] = response.RequestMessage?.RequestUri?.ToString() ?? url;
            _logger.LogInformation("Received HTTP {StatusCode} from {Host} after {TtfbMs:F1} ms (time to first byte)",
                statusCode, response.RequestMessage?.RequestUri?.Host ?? host, ttfbMs);

            // Read the body (capped) to measure the transfer phase
            var bytes = 0L;
            var buffer = new byte[8192];
            var transferStopwatch = Stopwatch.StartNew();
            await using (var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token))
            {
                int read;
                while (bytes < MaxBodyBytes && (read = await stream.ReadAsync(buffer, timeoutCts.Token)) > 0)
                {
                    bytes += read;
                }
            }
            transferStopwatch.Stop();
            metrics["transfer_ms"] = Math.Round(transferStopwatch.Elapsed.TotalMilliseconds, 2);
            metrics["bytes_read"] = bytes;
            metrics["bytes_truncated"] = bytes >= MaxBodyBytes;
            _logger.LogInformation("Transferred {Bytes} bytes in {TransferMs:F1} ms", bytes, transferStopwatch.Elapsed.TotalMilliseconds);

            var details = new List<string> { $"HTTP {statusCode} {response.ReasonPhrase} from {response.RequestMessage?.RequestUri?.Host ?? url}" };

            var statusOk = StatusMatches(assignment.ExpectedStatusCodes, statusCode);
            if (!statusOk)
            {
                details.Add($"expected status codes: {assignment.ExpectedStatusCodes}");
            }

            var certificateOk = true;
            if (serverCertificate != null)
            {
                var daysRemaining = (serverCertificate.NotAfter - DateTime.UtcNow).TotalDays;
                metrics["cert_subject"] = serverCertificate.Subject;
                metrics["cert_issuer"] = serverCertificate.Issuer;
                metrics["cert_expires"] = serverCertificate.NotAfter.ToString("yyyy-MM-dd");
                details.Add($"TLS certificate expires {serverCertificate.NotAfter:yyyy-MM-dd} ({daysRemaining:F0} days)");

                if (assignment.CheckCertificateExpiryDays is int threshold && daysRemaining < threshold)
                {
                    certificateOk = false;
                    details.Add($"expires within the {threshold}-day threshold");
                }
            }

            return new TestOutcome
            {
                Success = statusOk && certificateOk,
                Output = string.Join("; ", details),
                Metrics = metrics
            };
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"HTTP request failed: {ex.SocketErrorCode}", Metrics = metrics };
        }
        catch (AuthenticationException ex)
        {
            return new TestOutcome { Success = false, Output = $"TLS handshake failed: {ex.Message}", Metrics = metrics };
        }
        catch (HttpRequestException ex)
        {
            return new TestOutcome { Success = false, Output = $"HTTP request failed: {ex.Message}", Metrics = metrics };
        }
    }

    /// <summary>
    /// Checks a status code against an expectation like "200-399" or "200,301,302".
    /// </summary>
    private static bool StatusMatches(string expected, int statusCode)
    {
        foreach (var part in expected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = part.Split('-');
            if (range.Length == 2 && int.TryParse(range[0], out var from) && int.TryParse(range[1], out var to))
            {
                if (statusCode >= from && statusCode <= to)
                {
                    return true;
                }
            }
            else if (int.TryParse(part, out var code) && statusCode == code)
            {
                return true;
            }
        }
        return false;
    }
}
