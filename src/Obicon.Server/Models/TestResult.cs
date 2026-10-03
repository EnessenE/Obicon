using Obicon.Shared.Models.Results;

namespace Obicon.Server.Models;

/// <summary>
/// Result of a test execution.
/// </summary>
public class TestResult
{
    /// <summary>
    /// Indicates if the test execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Duration of the test execution in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Text output from the test execution. Default: null.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Structured details of the run, one populated section per test type. Null for
    /// results reported by nodes older than 0.4.0. Default: null.
    /// </summary>
    public TestResultDetails? Details { get; set; }
}
