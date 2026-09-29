using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Executes a single test of a given type against a target.
/// </summary>
public interface ITestRunner
{
    /// <summary>
    /// Type of test this runner executes.
    /// </summary>
    TestType Type { get; }

    /// <summary>
    /// Runs the test against the target. Must honor the cancellation token and complete
    /// within the timeout, or throw OperationCanceledException.
    /// </summary>
    /// <param name="target">The test target: URL, host, or host:port depending on type.</param>
    /// <param name="timeout">Maximum execution time for the test.</param>
    /// <param name="cancellationToken">Cancelled when the test times out or the node shuts down.</param>
    /// <returns>The outcome of the test.</returns>
    Task<TestOutcome> ExecuteAsync(string target, TimeSpan timeout, CancellationToken cancellationToken);
}
