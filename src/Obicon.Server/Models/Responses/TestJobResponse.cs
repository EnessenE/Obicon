using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Responses;

/// <summary>
/// A test job in the execution queue, as exposed by the API.
/// </summary>
public class TestJobResponse
{
    /// <summary>
    /// Unique identifier of the job.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the test this job belongs to. Guid.Empty for one-off runs without a saved test.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// ID of the node assigned to execute this job.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Type of test this job executes. See <see cref="TestType"/> for available types.
    /// </summary>
    public TestType TestType { get; set; }

    /// <summary>
    /// Target of the test. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Maximum execution time for this job in seconds. Default: 60.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Current status of the job. See <see cref="TestJobStatus"/> for available statuses.
    /// </summary>
    public TestJobStatus Status { get; set; }

    /// <summary>
    /// Timestamp when the job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the job execution started. Null if not yet started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the node acknowledged the assignment. Null if the node never responded.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Timestamp when the job was completed. Null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Whether the test execution succeeded. Null if not yet completed.
    /// </summary>
    public bool? Success { get; set; }

    /// <summary>
    /// Duration of the test execution in milliseconds. Null if not yet completed.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Text output of the test execution. Null if not yet completed.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error message if the job failed. Null if successful or not yet completed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed measurements of the run: resolved addresses, phase timings, nameservers,
    /// certificate details. Null if not yet completed. Default: null.
    /// </summary>
    public Dictionary<string, object>? Metrics { get; set; }

    /// <summary>
    /// Maps a TestJob entity to its API response.
    /// </summary>
    /// <param name="job">The job entity to map.</param>
    public static TestJobResponse From(Models.TestJob job) => new()
    {
        Id = job.Id,
        TestId = job.TestId,
        NodeId = job.NodeId,
        TestType = job.TestType,
        Target = job.Target,
        TimeoutSeconds = job.TimeoutSeconds,
        Status = job.Status,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        AcknowledgedAt = job.AcknowledgedAt,
        CompletedAt = job.CompletedAt,
        Success = job.Result?.Success,
        DurationMs = job.Result?.DurationMs,
        Output = job.Result?.Output,
        ErrorMessage = job.ErrorMessage,
        Metrics = job.Result?.Metrics
    };
}
