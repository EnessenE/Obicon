using System.Net;
using System.Net.NetworkInformation;
using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Traceroute test: increments the TTL of ICMP echo requests hop by hop until the target responds.
/// </summary>
public class TracerouteTestRunner : ITestRunner
{
    private const int MaxHops = 30;
    private const int PerHopTimeoutMs = 2000;

    /// <inheritdoc />
    public TestType Type => TestType.Traceroute;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(string target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var buffer = new byte[16];
        using var ping = new Ping();
        var hops = new List<string>(MaxHops);

        for (var ttl = 1; ttl <= MaxHops; ttl++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new PingOptions { Ttl = ttl, DontFragment = true };
            var reply = await ping.SendPingAsync(target, PerHopTimeoutMs, buffer, options);

            if (reply.Status == IPStatus.Success)
            {
                hops.Add($"{ttl,2}  {reply.Address}  {reply.RoundtripTime} ms");
                return new TestOutcome { Success = true, Output = string.Join(Environment.NewLine, hops) };
            }

            if (reply.Status == IPStatus.TtlExpired)
            {
                hops.Add($"{ttl,2}  {reply.Address}  {reply.RoundtripTime} ms");
            }
            else if (reply.Status == IPStatus.TimedOut)
            {
                hops.Add($"{ttl,2}  *");
            }
            else
            {
                hops.Add($"{ttl,2}  {reply.Status}");
                return new TestOutcome { Success = false, Output = string.Join(Environment.NewLine, hops) };
            }
        }

        return new TestOutcome { Success = false, Output = $"Max hops ({MaxHops}) reached:{Environment.NewLine}{string.Join(Environment.NewLine, hops)}" };
    }
}
