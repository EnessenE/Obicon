using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

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
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var separator = assignment.Target.LastIndexOf(':');
        var host = separator > 0 ? assignment.Target[..separator] : assignment.Target;
        var port = separator > 0 && int.TryParse(assignment.Target[(separator + 1)..], out var parsed) ? parsed : DefaultPort;

        IPAddress? address;
        try
        {
            address = await HostResolver.ResolveAsync(host, assignment.IpVersion, cancellationToken);
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"Could not resolve {host}: {ex.SocketErrorCode}" };
        }

        if (address == null)
        {
            return new TestOutcome { Success = false, Output = $"{host} has no {HostResolver.FamilyName(assignment.IpVersion)} address" };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            var connectStart = Stopwatch.GetTimestamp();
            await socket.ConnectAsync(new IPEndPoint(address, port), timeoutCts.Token);
            var elapsed = Stopwatch.GetElapsedTime(connectStart);

            return new TestOutcome { Success = true, Output = $"Connected to {host}:{port} ({address.AddressFamily}) in {elapsed.TotalMilliseconds:F0} ms" };
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"TCP connect to {host}:{port} failed: {ex.SocketErrorCode}" };
        }
    }
}
