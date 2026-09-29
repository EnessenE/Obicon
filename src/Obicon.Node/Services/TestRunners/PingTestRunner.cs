using System.Net.NetworkInformation;
using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// ICMP ping test.
/// </summary>
public class PingTestRunner : ITestRunner
{
    /// <inheritdoc />
    public TestType Type => TestType.Ping;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(string target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var ping = new Ping();
        var reply = await ping.SendPingAsync(target, (int)timeout.TotalMilliseconds);

        return reply.Status == IPStatus.Success
            ? new TestOutcome { Success = true, Output = $"Reply from {reply.Address}: time={reply.RoundtripTime}ms ttl={reply.Options?.Ttl}" }
            : new TestOutcome { Success = false, Output = $"Ping failed: {reply.Status}" };
    }
}
