namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent by node to report test execution results.
/// </summary>
public class TestResultMessage
{
    /// <summary>
    /// Unique identifier of the test job.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the test that was executed.
    /// </summary>
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the node that executed the test.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the test execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Duration of test execution in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Text output from the test execution. Default: null.
    /// </summary>
    public string? Output { get; set; }
}
