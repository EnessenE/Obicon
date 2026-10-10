namespace Obicon.Server.Models;

/// <summary>
/// How finished test jobs are stored in the database, set by the TestResultStorageMode
/// server setting. History beyond the retention window lives in the user's metric store.
/// </summary>
public enum TestResultStorageMode
{
    /// <summary>
    /// Finished jobs keep their complete result payload. This is the default.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Finished jobs keep their row skeleton (status, timestamps, ids) but the result
    /// payload is dropped on completion.
    /// </summary>
    MetadataOnly = 1,

    /// <summary>
    /// Finished job rows are deleted on completion; the queue holds only in-flight jobs.
    /// </summary>
    None = 2
}
