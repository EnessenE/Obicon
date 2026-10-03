using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;

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
        var details = new DnsDetails { Host = assignment.Target };

        if (IPAddress.TryParse(assignment.Target, out var literal))
        {
            // Literal IP: nothing to resolve
            var matches = HostResolver.Matches(literal, assignment.IpVersion);
            details.Resolved = [assignment.Target];
            details.Via = "literal";
            if (!matches)
            {
                details.Error = $"not {HostResolver.FamilyName(assignment.IpVersion)}";
            }

            return new TestOutcome
            {
                Success = matches,
                Output = matches
                    ? $"{assignment.Target} is a literal {literal.AddressFamily} address"
                    : $"{assignment.Target} is a literal {literal.AddressFamily} address, not {HostResolver.FamilyName(assignment.IpVersion)}",
                Details = new TestResultDetails { Dns = details }
            };
        }

        var nameservers = DnsQueryClient.GetSystemNameservers();
        if (nameservers.Count > 0)
        {
            details.NameserversQueried = nameservers.Select(n => n.ToString()).ToList();
            details.Via = "nameserver";
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

                details.AnsweringNameserver = result.Nameserver;
                details.NameserverRttMs = Math.Round(result.RttMs, 2);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var aRecords = string.Join(", ", result.A);
                    var aaaaRecords = string.Join(", ", result.Aaaa);
                    LogNameserverAnswered(result.Nameserver, result.RttMs, aRecords, aaaaRecords);
                }

                var resolved = Filter(result, assignment.IpVersion);
                details.ARecords = result.A;
                details.AaaaRecords = result.Aaaa;
                details.Resolved = resolved;

                return Evaluate(assignment, resolved, details, $"via nameserver {result.Nameserver}");
            }

            details.Error = "no nameserver responded";
            LogNoNameserverAnswered(assignment.Target);
        }
        else
        {
            details.Error = "no nameservers discovered";
        }

        // Fallback: the OS resolver, without visibility into which server answered
        LogResolvingThroughOs(assignment.Target);
        details.Via = "os-resolver";
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            var allAddresses = await Dns.GetHostAddressesAsync(assignment.Target, timeoutCts.Token);
            var resolved = assignment.IpVersion == IpVersion.Any
                ? allAddresses.ToList()
                : allAddresses.Where(a => HostResolver.Matches(a, assignment.IpVersion)).ToList();

            details.Resolved = resolved.Select(a => a.ToString()).ToList();
            return Evaluate(assignment, resolved.Select(a => a.ToString()).ToList(), details, "via the OS resolver");
        }
        catch (SocketException ex)
        {
            details.Error = ex.SocketErrorCode.ToString();
            return new TestOutcome
            {
                Success = false,
                Output = $"DNS resolution of {assignment.Target} failed: {ex.SocketErrorCode}",
                Details = new TestResultDetails { Dns = details }
            };
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

    private TestOutcome Evaluate(TestAssignmentMessage assignment, List<string> resolved, DnsDetails details, string via)
    {
        if (resolved.Count == 0)
        {
            details.Error ??= $"no {HostResolver.FamilyName(assignment.IpVersion)} address";
            return new TestOutcome
            {
                Success = false,
                Output = $"{assignment.Target} has no {HostResolver.FamilyName(assignment.IpVersion)} address ({via})",
                Details = new TestResultDetails { Dns = details }
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
                Details = new TestResultDetails { Dns = details }
            };
        }

        var matches = resolved.Any(r => string.Equals(r, assignment.ExpectedDnsResult, StringComparison.OrdinalIgnoreCase));
        details.ExpectedAddress = assignment.ExpectedDnsResult;
        details.ExpectedMatched = matches;
        LogResolvedWithExpectation(assignment.Target, resolvedText, assignment.ExpectedDnsResult, matches ? "match" : "mismatch");

        return new TestOutcome
        {
            Success = matches,
            Output = matches
                ? $"Resolved {assignment.Target} to {resolvedText} (expected {assignment.ExpectedDnsResult}) ({via})"
                : $"Resolved {assignment.Target} to {resolvedText} but expected {assignment.ExpectedDnsResult} ({via})",
            Details = new TestResultDetails { Dns = details }
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
