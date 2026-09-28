namespace Obicon.Shared.Models.Enums;

/// <summary>
/// Status of a test job during its lifecycle.
/// </summary>
public enum TestJobStatus
{
    /// <summary>
    /// Job is waiting in the queue to be assigned to a node.
    /// </summary>
    Queued,

    /// <summary>
    /// Job has been assigned to a node but not yet started.
    /// </summary>
    Assigned,

    /// <summary>
    /// Job is currently being executed by a node.
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// Job timed out before completion.
    /// </summary>
    Timeout
}
