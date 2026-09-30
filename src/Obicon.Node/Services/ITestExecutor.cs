using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services;

/// <summary>
/// Executes test assignments, enforcing the maximum concurrency and per-test timeouts.
/// </summary>
public interface ITestExecutor
{
    /// <summary>
    /// Execution statistics of this node.
    /// </summary>
    NodeStatistics Statistics { get; }

    /// <summary>
    /// Starts executing a test assignment on its own task. Returns once the assignment is accepted.
    /// </summary>
    /// <param name="assignment">The test assignment received from the server.</param>
    Task ExecuteAssignmentAsync(TestAssignmentMessage assignment);
}
