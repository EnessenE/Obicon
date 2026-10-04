using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Results;

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
    /// IP family this job runs against. A test with Both is scheduled as one IPv4 and
    /// one IPv6 job per node.
    /// </summary>
    public IpVersion IpVersion { get; set; }

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
    /// Structured details of the run, one populated section per test type (e.g.
    /// Traceroute with one record per hop). Null for old results or when not yet
    /// completed. Default: null.
    /// </summary>
    public TestResultDetails? Details { get; set; }

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
        IpVersion = job.IpVersion,
        Status = job.Status,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        AcknowledgedAt = job.AcknowledgedAt,
        CompletedAt = job.CompletedAt,
        Success = job.Success,
        DurationMs = job.DurationMs,
        Output = job.Output,
        ErrorMessage = job.ErrorMessage,
        Details = TestResultDetailsMapper.ToShared(job)
    };
}
