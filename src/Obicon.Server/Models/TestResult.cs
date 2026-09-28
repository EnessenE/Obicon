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
    /// Metrics collected during test execution. Default: null.
    /// </summary>
    public Dictionary<string, object>? Metrics { get; set; }
}
