using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Outcome of a single executed test.
/// </summary>
public class TestOutcome
{
    /// <summary>
    /// Indicates if the test target behaved as expected.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Human-readable output describing the test result. Default: empty string.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Structured details of the run, one populated section per test type. Default: null.
    /// </summary>
    public TestResultDetails? Details { get; init; }
}
