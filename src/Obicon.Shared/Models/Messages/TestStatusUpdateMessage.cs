using Obicon.Shared.Models.Enums;

namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent by node to update test execution status.
/// </summary>
public class TestStatusUpdateMessage
{
    /// <summary>
    /// Unique identifier of the test job.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the test that is being executed.
    /// </summary>
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the node executing the test.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the test execution. See <see cref="TestJobStatus"/>.
    /// </summary>
    public TestJobStatus Status { get; set; }

    /// <summary>
    /// Optional status message. Default: null.
    /// </summary>
    public string? Message { get; set; }
}
