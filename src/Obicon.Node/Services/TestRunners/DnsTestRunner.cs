using System.Net;
using System.Net.Sockets;
using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// DNS resolution test.
/// </summary>
public class DnsTestRunner : ITestRunner
{
    /// <inheritdoc />
    public TestType Type => TestType.Dns;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(string target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(target, timeoutCts.Token);
            return new TestOutcome
            {
                Success = addresses.Length > 0,
                Output = $"Resolved {target} to {string.Join(", ", addresses.Select(a => a.ToString()))}"
            };
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"DNS resolution of {target} failed: {ex.SocketErrorCode}" };
        }
    }
}
