using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

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

        var metrics = new Dictionary<string, object> { ["target"] = assignment.Target };

        IPAddress? address;
        try
        {
            var dnsStopwatch = System.Diagnostics.Stopwatch.StartNew();
            address = await HostResolver.ResolveAsync(assignment.Target, assignment.IpVersion, cancellationToken);
            dnsStopwatch.Stop();

            if (address == null)
            {
                LogNoAddress(assignment.Target, HostResolver.FamilyName(assignment.IpVersion));
                return new TestOutcome
                {
                    Success = false,
                    Output = $"{assignment.Target} has no {HostResolver.FamilyName(assignment.IpVersion)} address",
                    Metrics = metrics
                };
            }

            metrics["resolved"] = address.ToString();
            metrics["dns_ms"] = Math.Round(dnsStopwatch.Elapsed.TotalMilliseconds, 2);
            LogResolved(assignment.Target, address, dnsStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"Could not resolve {assignment.Target}: {ex.SocketErrorCode}", Metrics = metrics };
        }

        using var ping = new Ping();
        var pingStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var reply = await ping.SendPingAsync(address, (int)timeout.TotalMilliseconds);
        pingStopwatch.Stop();

        metrics["reply_from"] = reply.Address.ToString();
        metrics["reply_status"] = reply.Status.ToString();
        metrics["roundtrip_ms"] = reply.RoundtripTime;
        metrics["ttl"] = reply.Options?.Ttl.ToString(CultureInfo.InvariantCulture) ?? "";
        metrics["wallclock_ms"] = Math.Round(pingStopwatch.Elapsed.TotalMilliseconds, 2);
        LogPingReply(reply.Address, reply.Status, reply.RoundtripTime, reply.Options?.Ttl);

        return reply.Status == IPStatus.Success
            ? new TestOutcome { Success = true, Output = $"Reply from {reply.Address}: time={reply.RoundtripTime}ms ttl={reply.Options?.Ttl}", Metrics = metrics }
            : new TestOutcome { Success = false, Output = $"Ping failed: {reply.Status}", Metrics = metrics };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Target} has no {Family} address")]
    private partial void LogNoAddress(string target, string family);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Target} to {Address} in {DnsMs:F1} ms")]
    private partial void LogResolved(string target, IPAddress address, double dnsMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ping reply from {Address}: status={Status} roundtrip={Roundtrip}ms ttl={Ttl}")]
    private partial void LogPingReply(IPAddress address, IPStatus status, long roundtrip, int? ttl);
}
