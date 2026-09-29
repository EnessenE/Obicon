using System.Diagnostics;
using System.Net.Sockets;
using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// TCP connect test. Target format: host or host:port (default port 80).
/// </summary>
public class TcpTestRunner : ITestRunner
{
    private const int DefaultPort = 80;

    /// <inheritdoc />
    public TestType Type => TestType.Tcp;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(string target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var separator = target.LastIndexOf(':');
        var host = separator > 0 ? target[..separator] : target;
        var port = separator > 0 && int.TryParse(target[(separator + 1)..], out var parsed) ? parsed : DefaultPort;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            var connectStart = Stopwatch.GetTimestamp();
            await socket.ConnectAsync(host, port, timeoutCts.Token);
            var elapsed = Stopwatch.GetElapsedTime(connectStart);

            return new TestOutcome { Success = true, Output = $"Connected to {host}:{port} in {elapsed.TotalMilliseconds:F0} ms" };
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"TCP connect to {host}:{port} failed: {ex.SocketErrorCode}" };
        }
    }
}
