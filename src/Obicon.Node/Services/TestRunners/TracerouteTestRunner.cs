using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Traceroute test: increments the TTL of ICMP echo requests hop by hop until the target responds.
/// </summary>
public partial class TracerouteTestRunner : ITestRunner
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

        LogTracerouting(assignment.Target, address);

        var buffer = new byte[16];
        using var ping = new Ping();
        var hops = new List<TracerouteHop>(MaxHops);

        for (var ttl = 1; ttl <= MaxHops; ttl++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new PingOptions { Ttl = ttl, DontFragment = true };
            var reply = await ping.SendPingAsync(address, PerHopTimeoutMs, buffer, options);

            if (reply.Status == IPStatus.Success)
            {
                hops.Add(new TracerouteHop { Hop = ttl, Address = reply.Address.ToString(), Status = reply.Status.ToString(), RoundtripMs = reply.RoundtripTime });
                LogTargetReached(ttl);
                return TracerouteOutcome(success: true, address, targetReached: true, ttl, hops);
            }

            if (reply.Status == IPStatus.TtlExpired)
            {
                hops.Add(new TracerouteHop { Hop = ttl, Address = reply.Address.ToString(), Status = reply.Status.ToString(), RoundtripMs = reply.RoundtripTime });
                LogHop(ttl, reply.Address, reply.RoundtripTime);
            }
            else if (reply.Status == IPStatus.TimedOut)
            {
                hops.Add(new TracerouteHop { Hop = ttl, Status = reply.Status.ToString() });
                LogHopNoResponse(ttl);
            }
            else
            {
                hops.Add(new TracerouteHop { Hop = ttl, Status = reply.Status.ToString(), Error = reply.Status.ToString() });
                return TracerouteOutcome(success: false, address, targetReached: false, ttl, hops);
            }
        }

        return TracerouteOutcome(success: false, address, targetReached: false, MaxHops, hops, $"Max hops ({MaxHops}) reached");
    }

    /// <summary>
    /// Builds the traceroute outcome: the structured hop list as details, and the
    /// text output rendered from it.
    /// </summary>
    private static TestOutcome TracerouteOutcome(bool success, IPAddress address, bool targetReached, int hopCount, List<TracerouteHop> hops, string? prefix = null)
    {

        var lines = hops.Select(h => h.Address == null
            ? $"{h.Hop,2}  *"
            : $"{h.Hop,2}  {h.Address}  {h.RoundtripMs} ms");
        var output = (prefix == null ? string.Empty : prefix + ":" + Environment.NewLine) + string.Join(Environment.NewLine, lines);

        return new TestOutcome
        {
            Success = success,
            Output = output,
            Details = new TestResultDetails
            {
                Traceroute = new TracerouteDetails
                {
                    ResolvedAddress = address.ToString(),
                    TargetReached = targetReached,
                    HopCount = hopCount,
                    Hops = hops
                }
            }
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tracerouting to {Target} ({Address})")]
    private partial void LogTracerouting(string target, IPAddress address);

    [LoggerMessage(Level = LogLevel.Information, Message = "Target reached after {Hops} hop(s)")]
    private partial void LogTargetReached(int hops);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hop {Hop}: {Address} ({Roundtrip} ms)")]
    private partial void LogHop(int hop, IPAddress address, long roundtrip);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hop {Hop}: no response")]
    private partial void LogHopNoResponse(int hop);
}
