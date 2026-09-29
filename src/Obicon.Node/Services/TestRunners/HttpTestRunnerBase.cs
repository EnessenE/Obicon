using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Shared logic for HTTP and HTTPS tests: IP version pinning, expected status code
/// matching, and TLS certificate checks.
/// </summary>
public abstract class HttpTestRunnerBase : ITestRunner
{
    /// <inheritdoc />
    protected abstract string Scheme { get; }

    /// <inheritdoc />
    public abstract TestType Type { get; }

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var target = assignment.Target;
        var url = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? target
            : $"{Scheme}{target}";

        X509Certificate2? serverCertificate = null;

        // One client per run so the certificate capture cannot race with concurrent tests
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            ConnectCallback = (context, ct) => ConnectForcedAsync(context, assignment.IpVersion, ct),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, cert, _, _) =>
                {
                    serverCertificate = (X509Certificate2?)cert;
                    return true;
                }
            }
        };
        using var client = new HttpClient(handler)
        {
            // Timeout is enforced per test via the cancellation token
            Timeout = Timeout.InfiniteTimeSpan
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var statusCode = (int)response.StatusCode;
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
                Output = string.Join("; ", details)
            };
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"HTTP request failed: {ex.SocketErrorCode}" };
        }
        catch (HttpRequestException ex)
        {
            return new TestOutcome { Success = false, Output = $"HTTP request failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Connects to the endpoint, forcing the requested IP family when set.
    /// </summary>
    private static async ValueTask<Stream> ConnectForcedAsync(SocketsHttpConnectionContext context, IpVersion ipVersion, CancellationToken cancellationToken)
    {
        if (ipVersion == IpVersion.Any)
        {
            var anySocket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await anySocket.ConnectAsync(context.DnsEndPoint, cancellationToken);
            return new NetworkStream(anySocket, ownsSocket: true);
        }

        var address = await HostResolver.ResolveAsync(context.DnsEndPoint.Host, ipVersion, cancellationToken)
            ?? throw new SocketException((int)SocketError.HostNotFound);

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
        return new NetworkStream(socket, ownsSocket: true);
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
