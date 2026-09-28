using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models;

public class TestJob
{
    /// <summary>
    /// Unique identifier for the job. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the test this job belongs to.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// ID of the node assigned to execute this job.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Current status of the job. See <see cref="TestJobStatus"/> for available statuses. Default: Queued.
    /// </summary>
    public TestJobStatus Status { get; set; } = TestJobStatus.Queued;

    /// <summary>
    /// Timestamp when the job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the job execution started. Null if not yet started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the job was completed. Null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Result of the test execution. Null if not yet completed.
    /// </summary>
    public TestResult? Result { get; set; }

    /// <summary>
    /// Error message if the job failed. Null if successful or not yet completed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

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
