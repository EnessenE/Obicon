using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;
using PingReply = Obicon.Shared.Models.Results.PingReply;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// ICMP ping test. Configurable per test: probe count, per-probe timeout, and the
/// interval between probes; the result reports every reply plus loss and
/// min/avg/max round trip statistics.
/// </summary>
public partial class PingTestRunner : ITestRunner
{
    private const int DefaultCount = 4;
    private const int DefaultTimeoutMs = 2000;
    private const int DefaultIntervalMs = 0;

    private readonly ILogger<PingTestRunner> _logger;

    public PingTestRunner(ILogger<PingTestRunner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TestType Type => TestType.Ping;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = Math.Clamp(assignment.PingCount ?? DefaultCount, 1, 100);
        var timeoutMs = Math.Clamp(assignment.PingTimeoutMs ?? DefaultTimeoutMs, 100, 60_000);
        var intervalMs = Math.Clamp(assignment.PingIntervalMs ?? DefaultIntervalMs, 0, 10_000);

        var details = new PingDetails
        {
            Target = assignment.Target,
            Sent = count
        };

        IPAddress? address;
        try
        {
            var dnsStopwatch = System.Diagnostics.Stopwatch.StartNew();
            address = await HostResolver.ResolveAsync(assignment.Target, assignment.IpVersion, cancellationToken);
            dnsStopwatch.Stop();
            details.DnsMs = Math.Round(dnsStopwatch.Elapsed.TotalMilliseconds, 2);

            if (address == null)
            {
                LogNoAddress(assignment.Target, HostResolver.FamilyName(assignment.IpVersion));
                details.Error = $"no {HostResolver.FamilyName(assignment.IpVersion)} address";
                return new TestOutcome
                {
                    Success = false,
                    Output = $"{assignment.Target} has no {HostResolver.FamilyName(assignment.IpVersion)} address",
                    Details = new TestResultDetails { Ping = details }
                };
            }

            details.ResolvedAddress = address.ToString();
            LogResolved(assignment.Target, address, dnsStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException ex)
        {
            details.Error = ex.SocketErrorCode.ToString();
            return new TestOutcome
            {
                Success = false,
                Output = $"Could not resolve {assignment.Target}: {ex.SocketErrorCode}",
                Details = new TestResultDetails { Ping = details }
            };
        }

        LogPinging(assignment.Target, address, count, timeoutMs);

        using var ping = new Ping();
        var buffer = new byte[16];
        var lines = new List<string>(count);

        for (var probe = 0; probe < count; probe++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reply = await ping.SendPingAsync(address, timeoutMs, buffer);
            var replyDetails = new PingReply
            {
                ReplyAddress = reply.Address.ToString(),
                ReplyStatus = reply.Status.ToString(),
                RoundtripMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : null,
                Ttl = reply.Options?.Ttl
            };
            details.Replies.Add(replyDetails);
            lines.Add(reply.Status == IPStatus.Success
                ? $"Reply from {reply.Address}: time={reply.RoundtripTime}ms ttl={reply.Options?.Ttl}"
                : $"No reply: {reply.Status}");
            LogPingReply(reply.Address, reply.Status, reply.RoundtripTime, reply.Options?.Ttl);

            if (probe < count - 1 && intervalMs > 0)
            {
                try
                {
                    await Task.Delay(intervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // Statistics over the probes that got a reply, classic ping style
        var roundtrips = details.Replies.Where(r => r.RoundtripMs != null).Select(r => r.RoundtripMs!.Value).ToList();
        details.Received = roundtrips.Count;
        details.LossPercent = Math.Round(100.0 * (count - roundtrips.Count) / count, 1);
        if (roundtrips.Count > 0)
        {
            details.MinRoundtripMs = roundtrips.Min();
            details.MaxRoundtripMs = roundtrips.Max();
            details.AvgRoundtripMs = Math.Round(roundtrips.Average(), 1);
            details.ReplyAddress = details.Replies.First(r => r.RoundtripMs != null).ReplyAddress;
            details.ReplyStatus = IPStatus.Success.ToString();
            details.RoundtripMs = details.AvgRoundtripMs;
            details.Ttl = details.Replies.First(r => r.RoundtripMs != null).Ttl;
        }
        else if (details.Replies.Count > 0)
        {
            details.ReplyAddress = details.Replies[0].ReplyAddress;
            details.ReplyStatus = details.Replies[0].ReplyStatus;
        }

        var summary = $"{count} sent, {details.Received} received, {details.LossPercent}% loss"
            + (roundtrips.Count > 0 ? $", min/avg/max = {details.MinRoundtripMs}/{details.AvgRoundtripMs}/{details.MaxRoundtripMs} ms" : string.Empty);
        lines.Add(summary);

        // Classic ping semantics: the run succeeds when at least one probe got a reply
        return new TestOutcome
        {
            Success = details.Received > 0,
            Output = string.Join(Environment.NewLine, lines),
            Details = new TestResultDetails { Ping = details }
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Target} has no {Family} address")]
    private partial void LogNoAddress(string target, string family);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Target} to {Address} in {DnsMs:F1} ms")]
    private partial void LogResolved(string target, IPAddress address, double dnsMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pinging {Target} ({Address}): {Count} probe(s), {TimeoutMs} ms per probe")]
    private partial void LogPinging(string target, IPAddress address, int count, int timeoutMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ping reply from {Address}: status={Status} roundtrip={Roundtrip}ms ttl={Ttl}")]
    private partial void LogPingReply(IPAddress address, IPStatus status, long roundtrip, int? ttl);
}
