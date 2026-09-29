using System.Net;
using System.Net.Sockets;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// DNS resolution test, optionally verified against an expected result.
/// </summary>
public class DnsTestRunner : ITestRunner
{
    /// <inheritdoc />
    public TestType Type => TestType.Dns;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var allAddresses = await Dns.GetHostAddressesAsync(assignment.Target, timeoutCts.Token);
            var addresses = allAddresses.Where(a => HostResolver.Matches(a, assignment.IpVersion)).ToArray();

            if (addresses.Length == 0)
            {
                return new TestOutcome
                {
                    Success = false,
                    Output = $"{assignment.Target} has no {HostResolver.FamilyName(assignment.IpVersion)} address"
                };
            }

            var resolved = string.Join(", ", addresses.Select(a => a.ToString()));

            if (addresses.Length == 0)
            {
                return new TestOutcome { Success = false, Output = $"Resolved {assignment.Target} to nothing" };
            }

            if (string.IsNullOrWhiteSpace(assignment.ExpectedDnsResult))
            {
                return new TestOutcome { Success = true, Output = $"Resolved {assignment.Target} to {resolved}" };
            }

            var matches = addresses.Any(a => string.Equals(a.ToString(), assignment.ExpectedDnsResult, StringComparison.OrdinalIgnoreCase));
            return new TestOutcome
            {
                Success = matches,
                Output = matches
                    ? $"Resolved {assignment.Target} to {resolved} (expected {assignment.ExpectedDnsResult})"
                    : $"Resolved {assignment.Target} to {resolved} but expected {assignment.ExpectedDnsResult}"
            };
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"DNS resolution of {assignment.Target} failed: {ex.SocketErrorCode}" };
        }
    }
}
