using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// ICMP ping test.
/// </summary>
public partial class PingTestRunner : ITestRunner
{
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

        var details = new PingDetails { Target = assignment.Target };

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

        using var ping = new Ping();
        var pingStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var reply = await ping.SendPingAsync(address, (int)timeout.TotalMilliseconds);
        pingStopwatch.Stop();

        details.ReplyAddress = reply.Address.ToString();
        details.ReplyStatus = reply.Status.ToString();
        details.RoundtripMs = reply.RoundtripTime;
        details.Ttl = reply.Options?.Ttl;
        details.WallclockMs = Math.Round(pingStopwatch.Elapsed.TotalMilliseconds, 2);

        LogPingReply(reply.Address, reply.Status, reply.RoundtripTime, reply.Options?.Ttl);

        return reply.Status == IPStatus.Success
            ? new TestOutcome { Success = true, Output = $"Reply from {reply.Address}: time={reply.RoundtripTime}ms ttl={reply.Options?.Ttl}", Details = new TestResultDetails { Ping = details } }
            : new TestOutcome { Success = false, Output = $"Ping failed: {reply.Status}", Details = new TestResultDetails { Ping = details } };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Target} has no {Family} address")]
    private partial void LogNoAddress(string target, string family);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Target} to {Address} in {DnsMs:F1} ms")]
    private partial void LogResolved(string target, IPAddress address, double dnsMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ping reply from {Address}: status={Status} roundtrip={Roundtrip}ms ttl={Ttl}")]
    private partial void LogPingReply(IPAddress address, IPStatus status, long roundtrip, int? ttl);
}
