using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Traceroute test: increments the TTL of ICMP echo requests hop by hop until the
/// target responds. Configurable per test: hop limit, probes per hop, per-probe
/// timeout, and reverse hostname resolution.
/// </summary>
public partial class TracerouteTestRunner : ITestRunner
{
    private const int DefaultMaxHops = 30;
    private const int DefaultQueriesPerHop = 3;
    private const int DefaultQueryTimeoutMs = 2000;
    private const bool DefaultResolveHostnames = true;

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

        var maxHops = Math.Clamp(assignment.TracerouteMaxHops ?? DefaultMaxHops, 1, 64);
        var queriesPerHop = Math.Clamp(assignment.TracerouteQueriesPerHop ?? DefaultQueriesPerHop, 1, 10);
        var queryTimeoutMs = Math.Clamp(assignment.TracerouteQueryTimeoutMs ?? DefaultQueryTimeoutMs, 100, 60_000);
        var resolveHostnames = assignment.TracerouteResolveHostnames ?? DefaultResolveHostnames;
        LogTracerouting(assignment.Target, address, maxHops, queriesPerHop, queryTimeoutMs);

        var buffer = new byte[16];
        using var ping = new Ping();
        var hops = new List<TracerouteHop>(maxHops);

        for (var ttl = 1; ttl <= maxHops; ttl++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // One hop = several probes; classic traceroute reports each probe's round trip
            var replies = new List<PingReply>(queriesPerHop);
            for (var query = 0; query < queriesPerHop; query++)
            {
                var options = new PingOptions { Ttl = ttl, DontFragment = true };
                replies.Add(await ping.SendPingAsync(address, queryTimeoutMs, buffer, options));
            }

            var hop = BuildHop(ttl, replies);

            if (replies.Any(r => r.Status == IPStatus.Success))
            {
                hop.Address = replies.First(r => r.Status == IPStatus.Success).Address.ToString();
                hop.Status = IPStatus.Success.ToString();
                hops.Add(hop);
                LogTargetReached(ttl);
                return await TracerouteOutcomeAsync(success: true, address, targetReached: true, ttl, hops, resolveHostnames, cancellationToken);
            }

            if (replies.FirstOrDefault(r => r.Status == IPStatus.TtlExpired) is { } router)
            {
                hop.Address = router.Address.ToString();
                hop.Status = IPStatus.TtlExpired.ToString();
                hops.Add(hop);
                LogHop(ttl, router.Address, hop.RoundtripMs ?? 0);
            }
            else if (replies.All(r => r.Status == IPStatus.TimedOut))
            {
                hops.Add(hop);
                LogHopNoResponse(ttl);
            }
            else
            {
                var other = replies.First(r => r.Status != IPStatus.TimedOut);
                hop.Status = other.Status.ToString();
                hop.Error = other.Status.ToString();
                hops.Add(hop);
                return await TracerouteOutcomeAsync(success: false, address, targetReached: false, ttl, hops, resolveHostnames, cancellationToken);
            }
        }

        return await TracerouteOutcomeAsync(success: false, address, targetReached: false, maxHops, hops, resolveHostnames, cancellationToken, $"Max hops ({maxHops}) reached");
    }

    /// <summary>
    /// Builds the hop record from the probes: one entry per probe with its own
    /// round trip, and the average of the answered ones on the hop itself.
    /// </summary>
    private static TracerouteHop BuildHop(int ttl, List<PingReply> replies)
    {
        var hop = new TracerouteHop
        {
            Hop = ttl,
            Status = IPStatus.TimedOut.ToString(),
            Probes = [.. replies.Select(r => new TracerouteProbe
            {
                Status = r.Status.ToString(),
                RoundtripMs = r.Status is IPStatus.TtlExpired or IPStatus.Success ? r.RoundtripTime : null
            })]
        };

        var answered = hop.Probes.Where(p => p.RoundtripMs != null).Select(p => p.RoundtripMs!.Value).ToList();
        if (answered.Count > 0)
        {
            hop.RoundtripMs = Math.Round(answered.Average(), 1);
        }

        return hop;
    }

    /// <summary>
    /// Builds the traceroute outcome: the structured hop list as details (hostnames
    /// resolved when enabled) and the text output rendered from it.
    /// </summary>
    private static async Task<TestOutcome> TracerouteOutcomeAsync(
        bool success,
        IPAddress address,
        bool targetReached,
        int hopCount,
        List<TracerouteHop> hops,
        bool resolveHostnames,
        CancellationToken cancellationToken,
        string? prefix = null)
    {
        if (resolveHostnames)
        {
            await ResolveHopHostnamesAsync(hops, cancellationToken);
        }

        var lines = hops.Select(h =>
        {
            var target = h.Hostname != null
                ? $"{h.Hostname} ({h.Address})"
                : h.Address ?? "*";
            var rtts = h.Probes.Where(p => p.RoundtripMs != null).Select(p => $"{p.RoundtripMs}").ToList();
            return $"{h.Hop,2}  {target}  {(rtts.Count > 0 ? string.Join(" ms  ", rtts) + " ms" : "*")}";
        });
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

    /// <summary>
    /// Resolves each hop's address to a hostname, best effort: a missing PTR record
    /// or a failed lookup leaves the hostname null.
    /// </summary>
    private static async Task ResolveHopHostnamesAsync(List<TracerouteHop> hops, CancellationToken cancellationToken)
    {
        foreach (var hop in hops.Where(h => h.Address != null))
        {
            try
            {
                // A string IP argument makes this a reverse lookup
                var entry = await Dns.GetHostEntryAsync(hop.Address!, cancellationToken);
                hop.Hostname = entry.HostName;
            }
            catch (SocketException)
            {
                // Best effort: leave the hostname unset
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tracerouting to {Target} ({Address}): up to {MaxHops} hops, {QueriesPerHop} probe(s) per hop, {TimeoutMs} ms per probe")]
    private partial void LogTracerouting(string target, IPAddress address, int maxHops, int queriesPerHop, int timeoutMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Target reached after {Hops} hop(s)")]
    private partial void LogTargetReached(int hops);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hop {Hop}: {Address} ({Roundtrip} ms)")]
    private partial void LogHop(int hop, IPAddress address, double roundtrip);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hop {Hop}: no response")]
    private partial void LogHopNoResponse(int hop);
}
