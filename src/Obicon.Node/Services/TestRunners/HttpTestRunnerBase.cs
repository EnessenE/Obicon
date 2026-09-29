using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Shared HTTP client and logic for HTTP and HTTPS tests.
/// </summary>
public abstract class HttpTestRunnerBase : ITestRunner
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true
    })
    {
        // Timeout is enforced per test via the cancellation token
        Timeout = Timeout.InfiniteTimeSpan
    };

    /// <summary>
    /// URL scheme to prepend when the target has no scheme. Derived classes set this.
    /// </summary>
    protected abstract string Scheme { get; }

    /// <inheritdoc />
    public abstract TestType Type { get; }

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(string target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var url = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? target
            : $"{Scheme}://{target}";

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var success = (int)response.StatusCode is >= 200 and < 400;
            return new TestOutcome
            {
                Success = success,
                Output = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} from {response.RequestMessage?.RequestUri?.Host ?? url}"
            };
        }
        catch (HttpRequestException ex)
        {
            return new TestOutcome { Success = false, Output = $"HTTP request failed: {ex.Message}" };
        }
    }
}
