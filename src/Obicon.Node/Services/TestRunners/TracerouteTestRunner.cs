using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Traceroute test: increments the TTL of ICMP echo requests hop by hop until the target responds.
/// </summary>
public class TracerouteTestRunner : ITestRunner
{
    private const int MaxHops = 30;
    private const int PerHopTimeoutMs = 2000;

    private readonly ILogger<TracerouteTestRunner> _logger;

    public TracerouteTestRunner(ILogger<TracerouteTestRunner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TestType Type => TestType.Traceroute;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
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

        var metrics = new Dictionary<string, object>
        {
            ["target"] = assignment.Target,
            ["resolved"] = address.ToString()
        };
        _logger.LogInformation("Tracerouting to {Target} ({Address})", assignment.Target, address);

        var buffer = new byte[16];
        using var ping = new Ping();
        var hops = new List<string>(MaxHops);

        for (var ttl = 1; ttl <= MaxHops; ttl++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new PingOptions { Ttl = ttl, DontFragment = true };
            var reply = await ping.SendPingAsync(address, PerHopTimeoutMs, buffer, options);

            if (reply.Status == IPStatus.Success)
            {
                hops.Add($"{ttl,2}  {reply.Address}  {reply.RoundtripTime} ms");
                metrics["hop_count"] = ttl;
                metrics["target_reached"] = true;
                _logger.LogInformation("Target reached after {Hops} hop(s)", ttl);
                return new TestOutcome { Success = true, Output = string.Join(Environment.NewLine, hops), Metrics = metrics };
            }

            if (reply.Status == IPStatus.TtlExpired)
            {
                hops.Add($"{ttl,2}  {reply.Address}  {reply.RoundtripTime} ms");
                _logger.LogDebug("Hop {Hop}: {Address} ({Roundtrip} ms)", ttl, reply.Address, reply.RoundtripTime);
            }
            else if (reply.Status == IPStatus.TimedOut)
            {
                hops.Add($"{ttl,2}  *");
                _logger.LogDebug("Hop {Hop}: no response", ttl);
            }
            else
            {
                hops.Add($"{ttl,2}  {reply.Status}");
                metrics["hop_count"] = ttl;
                metrics["target_reached"] = false;
                return new TestOutcome { Success = false, Output = string.Join(Environment.NewLine, hops), Metrics = metrics };
            }
        }

        metrics["hop_count"] = MaxHops;
        metrics["target_reached"] = false;
        return new TestOutcome { Success = false, Output = $"Max hops ({MaxHops}) reached:{Environment.NewLine}{string.Join(Environment.NewLine, hops)}", Metrics = metrics };
    }
}
