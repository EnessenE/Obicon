using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// DNS resolution test. Queries the system's nameservers directly so the result
/// reports which server answered; falls back to the OS resolver when no
/// nameservers can be discovered.
/// </summary>
public partial class DnsTestRunner : ITestRunner
{
    private readonly ILogger<DnsTestRunner> _logger;

    public DnsTestRunner(ILogger<DnsTestRunner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TestType Type => TestType.Dns;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var metrics = new Dictionary<string, object> { ["host"] = assignment.Target };

        if (IPAddress.TryParse(assignment.Target, out var literal))
        {
            // Literal IP: nothing to resolve
            var matches = HostResolver.Matches(literal, assignment.IpVersion);
            return new TestOutcome
            {
                Success = matches,
                Output = matches
                    ? $"{assignment.Target} is a literal {literal.AddressFamily} address"
                    : $"{assignment.Target} is a literal {literal.AddressFamily} address, not {HostResolver.FamilyName(assignment.IpVersion)}",
                Metrics = metrics
            };
        }

        var nameservers = DnsQueryClient.GetSystemNameservers();
        if (nameservers.Count > 0)
        {
            metrics["nameservers_queried"] = string.Join(",", nameservers);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var nameserversText = string.Join(", ", nameservers);
                LogQueryingNameservers(nameservers.Count, assignment.Target, nameserversText);
            }

            // Ask each nameserver; use the first one that answers with usable records
            var perServerTimeout = TimeSpan.FromMilliseconds(Math.Max(1000, timeout.TotalMilliseconds / nameservers.Count));
            foreach (var nameserver in nameservers)
            {
                var result = await DnsQueryClient.QueryAsync(nameserver, assignment.Target, perServerTimeout, cancellationToken);
                if (result.Error != null)
                {
                    LogNameserverFailed(nameserver, result.Error);
                    continue;
                }

                metrics["answering_nameserver"] = result.Nameserver;
                metrics["nameserver_rtt_ms"] = Math.Round(result.RttMs, 2);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var aRecords = string.Join(", ", result.A);
                    var aaaaRecords = string.Join(", ", result.Aaaa);
                    LogNameserverAnswered(result.Nameserver, result.RttMs, aRecords, aaaaRecords);
                }

                var resolved = Filter(result, assignment.IpVersion);
                metrics["a_records"] = string.Join(",", result.A);
                metrics["aaaa_records"] = string.Join(",", result.Aaaa);
                metrics["resolved"] = string.Join(",", resolved);

                return Evaluate(assignment, resolved, metrics, $"via nameserver {result.Nameserver}");
            }

            metrics["raw_query_error"] = "no nameserver responded";
            LogNoNameserverAnswered(assignment.Target);
        }
        else
        {
            metrics["raw_query_error"] = "no nameservers discovered";
        }

        // Fallback: the OS resolver, without visibility into which server answered
        LogResolvingThroughOs(assignment.Target);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            var allAddresses = await Dns.GetHostAddressesAsync(assignment.Target, timeoutCts.Token);
            var resolved = assignment.IpVersion == IpVersion.Any
                ? allAddresses.ToList()
                : allAddresses.Where(a => HostResolver.Matches(a, assignment.IpVersion)).ToList();

            metrics["resolved"] = string.Join(",", resolved.Select(a => a.ToString()));
            return Evaluate(assignment, resolved.Select(a => a.ToString()).ToList(), metrics, "via the OS resolver");
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"DNS resolution of {assignment.Target} failed: {ex.SocketErrorCode}", Metrics = metrics };
        }
    }

    private static List<string> Filter(NameserverResult result, IpVersion ipVersion)
    {
        return ipVersion switch
        {
            IpVersion.Ipv4 => result.A,
            IpVersion.Ipv6 => result.Aaaa,
            _ => result.A.Concat(result.Aaaa).ToList()
        };
    }

    private TestOutcome Evaluate(TestAssignmentMessage assignment, List<string> resolved, Dictionary<string, object> metrics, string via)
    {
        if (resolved.Count == 0)
        {
            return new TestOutcome
            {
                Success = false,
                Output = $"{assignment.Target} has no {HostResolver.FamilyName(assignment.IpVersion)} address ({via})",
                Metrics = metrics
            };
        }

        var resolvedText = string.Join(", ", resolved);

        if (string.IsNullOrWhiteSpace(assignment.ExpectedDnsResult))
        {
            LogResolved(assignment.Target, resolvedText, via);
            return new TestOutcome
            {
                Success = true,
                Output = $"Resolved {assignment.Target} to {resolvedText} ({via})",
                Metrics = metrics
            };
        }

        var matches = resolved.Any(r => string.Equals(r, assignment.ExpectedDnsResult, StringComparison.OrdinalIgnoreCase));
        LogResolvedWithExpectation(assignment.Target, resolvedText, assignment.ExpectedDnsResult, matches ? "match" : "mismatch");

        return new TestOutcome
        {
            Success = matches,
            Output = matches
                ? $"Resolved {assignment.Target} to {resolvedText} (expected {assignment.ExpectedDnsResult}) ({via})"
                : $"Resolved {assignment.Target} to {resolvedText} but expected {assignment.ExpectedDnsResult} ({via})",
            Metrics = metrics
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Querying {Count} nameserver(s) for {Host}: {Nameservers}")]
    private partial void LogQueryingNameservers(int count, string host, string nameservers);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nameserver {Nameserver} failed: {Error}")]
    private partial void LogNameserverFailed(IPAddress nameserver, string? error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nameserver {Nameserver} answered in {Rtt:F1} ms: A=[{A}] AAAA=[{Aaaa}]")]
    private partial void LogNameserverAnswered(string nameserver, double rtt, string a, string aaaa);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No nameserver answered for {Host}; falling back to the OS resolver")]
    private partial void LogNoNameserverAnswered(string host);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolving {Host} through the OS resolver")]
    private partial void LogResolvingThroughOs(string host);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Host} to {Resolved} ({Via})")]
    private partial void LogResolved(string host, string resolved, string via);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Host} to {Resolved}, expected {Expected}: {Matches}")]
    private partial void LogResolvedWithExpectation(string host, string resolved, string? expected, string matches);
}
