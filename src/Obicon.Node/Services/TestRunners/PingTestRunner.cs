using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// ICMP ping test.
/// </summary>
public class PingTestRunner : ITestRunner
{
    /// <inheritdoc />
    public TestType Type => TestType.Ping;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IPAddress? address;
        try
        {
            address = await HostResolver.ResolveAsync(assignment.Target, assignment.IpVersion, cancellationToken);
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"Could not resolve {assignment.Target}: {ex.SocketErrorCode}" };
        }

        if (address == null)
        {
            return new TestOutcome { Success = false, Output = $"{assignment.Target} has no {HostResolver.FamilyName(assignment.IpVersion)} address" };
        }

        using var ping = new Ping();
        var reply = await ping.SendPingAsync(address, (int)timeout.TotalMilliseconds);

        return reply.Status == IPStatus.Success
            ? new TestOutcome { Success = true, Output = $"Reply from {reply.Address}: time={reply.RoundtripTime}ms ttl={reply.Options?.Ttl}" }
            : new TestOutcome { Success = false, Output = $"Ping failed: {reply.Status}" };
    }
}
