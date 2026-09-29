using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

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
    /// Runs the test described by the assignment. Must honor the cancellation token and complete
    /// within the timeout, or throw OperationCanceledException.
    /// </summary>
    /// <param name="assignment">The assignment with target and per-type expectations.</param>
    /// <param name="timeout">Maximum execution time for the test.</param>
    /// <param name="cancellationToken">Cancelled when the test times out or the node shuts down.</param>
    /// <returns>The outcome of the test.</returns>
    Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken);
}
