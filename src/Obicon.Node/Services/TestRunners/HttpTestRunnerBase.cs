using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Shared logic for HTTP and HTTPS tests. Owns the connection so every phase is timed:
/// DNS resolution, TCP connect, TLS handshake, time to first byte, and transfer.
/// Also matches expected status codes and checks TLS certificate expiry.
/// </summary>
public abstract partial class HttpTestRunnerBase : ITestRunner
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

        // Cache busting appends a unique query parameter so caches serve a fresh response
        if (assignment.CacheBust)
        {
            var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            url = $"{url}{separator}_cb={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        var httpDetails = new HttpDetails { Url = url, ProxyUrl = assignment.ProxyUrl };
        var host = new Uri(url).Host;

        X509Certificate2? serverCertificate = null;
        double dnsMs = -1, connectMs = -1, tlsMs = -1;

        SocketsHttpHandler handler;
        if (!string.IsNullOrWhiteSpace(assignment.ProxyUrl))
        {
            // Through a proxy the per-phase timings cannot be attributed cleanly, so the
            // timing ConnectCallback is skipped; the total duration still covers the whole run
            handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                Proxy = new WebProxy(assignment.ProxyUrl),
                UseProxy = true
            };
        }
        else
        {
            handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                ConnectCallback = async (context, ct) =>
                {
                    var dnsStopwatch = Stopwatch.StartNew();
                    var resolvedAddress = await HostResolver.ResolveAsync(context.DnsEndPoint.Host, assignment.IpVersion, ct)
                        ?? throw new SocketException((int)SocketError.HostNotFound);
                    dnsStopwatch.Stop();
                    dnsMs = dnsStopwatch.Elapsed.TotalMilliseconds;
                    LogDnsResolved(context.DnsEndPoint.Host, resolvedAddress, dnsMs);
                    httpDetails.ResolvedAddress = resolvedAddress.ToString();
                    httpDetails.DnsMs = Math.Round(dnsMs, 2);

                    var socket = new Socket(resolvedAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    var connectStopwatch = Stopwatch.StartNew();
                    await socket.ConnectAsync(new IPEndPoint(resolvedAddress, context.DnsEndPoint.Port), ct);
                    connectStopwatch.Stop();
                    connectMs = connectStopwatch.Elapsed.TotalMilliseconds;
                    LogTcpConnected(context.DnsEndPoint.Host, context.DnsEndPoint.Port, connectMs);
                    httpDetails.ConnectMs = Math.Round(connectMs, 2);

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
                    LogTlsHandshake(context.DnsEndPoint.Host, tlsMs, sslStream.SslProtocol, sslStream.NegotiatedCipherSuite);
                    httpDetails.TlsMs = Math.Round(tlsMs, 2);
                    httpDetails.TlsProtocol = sslStream.SslProtocol.ToString();
                    httpDetails.TlsCipher = sslStream.NegotiatedCipherSuite.ToString();

                    return sslStream;
                }
            };
        }
        // HttpClient disposes the handler it is constructed with
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
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Custom headers from the test configuration, e.g. authentication headers
            if (assignment.Headers is { Count: > 0 } headers)
            {
                foreach (var header in headers)
                {
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        LogHeaderNotSet(header.Key);
                    }
                }
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var statusCode = (int)response.StatusCode;
            var ttfbMs = totalStopwatch.Elapsed.TotalMilliseconds;
            httpDetails.StatusCode = statusCode;
            httpDetails.ReasonPhrase = response.ReasonPhrase;
            httpDetails.FinalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
            httpDetails.TtfbMs = Math.Round(ttfbMs, 2);
            LogReceivedResponse(statusCode, response.RequestMessage?.RequestUri?.Host ?? host, ttfbMs);

            // Read the body (capped) to measure the transfer phase and check it against the pattern
            var transferStopwatch = Stopwatch.StartNew();
            var body = new MemoryStream();
            {
                var buffer = new byte[8192];
                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                int read;
                while (body.Length < MaxBodyBytes && (read = await stream.ReadAsync(buffer, timeoutCts.Token)) > 0)
                {
                    await body.WriteAsync(buffer.AsMemory(0, read), timeoutCts.Token);
                }
            }
            transferStopwatch.Stop();
            httpDetails.TransferMs = Math.Round(transferStopwatch.Elapsed.TotalMilliseconds, 2);
            httpDetails.BytesRead = body.Length;
            httpDetails.BytesTruncated = body.Length >= MaxBodyBytes;
            LogTransferred(body.Length, transferStopwatch.Elapsed.TotalMilliseconds);

            var details = new List<string> { $"HTTP {statusCode} {response.ReasonPhrase} from {response.RequestMessage?.RequestUri?.Host ?? url}" };

            var statusOk = StatusMatches(assignment.ExpectedStatusCodes, statusCode);
            if (!statusOk)
            {
                details.Add($"expected status codes: {assignment.ExpectedStatusCodes}");
            }

            // Body pattern check: the run fails when the body does not match the regex
            var bodyOk = true;
            if (!string.IsNullOrWhiteSpace(assignment.ExpectedBodyPattern))
            {
                var bodyText = Encoding.UTF8.GetString(body.ToArray());
                try
                {
                    bodyOk = Regex.IsMatch(bodyText, assignment.ExpectedBodyPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (!bodyOk)
                    {
                        details.Add($"body did not match pattern: {assignment.ExpectedBodyPattern}");
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    bodyOk = false;
                    details.Add($"body pattern check timed out: {assignment.ExpectedBodyPattern}");
                }
                httpDetails.BodyMatched = bodyOk;
            }

            var certificateOk = true;
            if (serverCertificate != null)
            {
                var daysRemaining = (serverCertificate.NotAfter - DateTime.UtcNow).TotalDays;
                httpDetails.Certificate = new CertificateDetails
                {
                    Subject = serverCertificate.Subject,
                    Issuer = serverCertificate.Issuer,
                    NotAfter = DateTime.SpecifyKind(serverCertificate.NotAfter, DateTimeKind.Utc),
                    DaysRemaining = Math.Round(daysRemaining, 1)
                };
                details.Add($"TLS certificate expires {serverCertificate.NotAfter:yyyy-MM-dd} ({daysRemaining:F0} days)");

                if (assignment.CheckCertificateExpiryDays is int threshold && daysRemaining < threshold)
                {
                    certificateOk = false;
                    details.Add($"expires within the {threshold}-day threshold");
                }
            }

            return new TestOutcome
            {
                Success = statusOk && certificateOk && bodyOk,
                Output = string.Join("; ", details),
                Details = new TestResultDetails { Http = httpDetails }
            };
        }
        catch (SocketException ex)
        {
            httpDetails.Error = ex.SocketErrorCode.ToString();
            return new TestOutcome { Success = false, Output = $"HTTP request failed: {ex.SocketErrorCode}", Details = new TestResultDetails { Http = httpDetails } };
        }
        catch (AuthenticationException ex)
        {
            httpDetails.Error = ex.Message;
            return new TestOutcome { Success = false, Output = $"TLS handshake failed: {ex.Message}", Details = new TestResultDetails { Http = httpDetails } };
        }
        catch (HttpRequestException ex)
        {
            httpDetails.Error = ex.Message;
            return new TestOutcome { Success = false, Output = $"HTTP request failed: {ex.Message}", Details = new TestResultDetails { Http = httpDetails } };
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

    [LoggerMessage(Level = LogLevel.Information, Message = "DNS resolved {Host} to {Address} in {DnsMs:F1} ms")]
    private partial void LogDnsResolved(string host, IPAddress address, double dnsMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "TCP connected to {Host}:{Port} in {ConnectMs:F1} ms")]
    private partial void LogTcpConnected(string host, int port, double connectMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "TLS handshake with {Host} finished in {TlsMs:F1} ms ({Protocol}, {Cipher})")]
    private partial void LogTlsHandshake(string host, double tlsMs, SslProtocols protocol, TlsCipherSuite cipher);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not set header {HeaderName} on the request")]
    private partial void LogHeaderNotSet(string headerName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received HTTP {StatusCode} from {Host} after {TtfbMs:F1} ms (time to first byte)")]
    private partial void LogReceivedResponse(int statusCode, string host, double ttfbMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transferred {Bytes} bytes in {TransferMs:F1} ms")]
    private partial void LogTransferred(long bytes, double transferMs);
}
