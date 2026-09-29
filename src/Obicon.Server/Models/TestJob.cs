using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models;

/// <summary>
/// Represents a test execution job in the queue.
/// </summary>
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
    /// Type of test this job executes. See <see cref="TestType"/> for available types.
    /// </summary>
    public TestType TestType { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Maximum execution time for this job in seconds. Default: 60.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days. Null disables the check.
    /// </summary>
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses. Null accepts any result.
    /// </summary>
    public string? ExpectedDnsResult { get; set; }

    /// <summary>
    /// IP version the test should use. Default: Any.
    /// </summary>
    public IpVersion IpVersion { get; set; } = IpVersion.Any;

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
    /// Timestamp when the node acknowledged the assignment. Null if the node never responded.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

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
