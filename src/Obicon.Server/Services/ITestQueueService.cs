using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

public interface ITestQueueService
{
    Task<TestJob> EnqueueJobAsync(Models.TestJob job);

    /// <summary>
    /// Creates a job directly with the given context, clamping its timeout to the
    /// server's MaxTestTimeoutSeconds, so callers that already hold a context for
    /// their own unit of work reuse it instead of opening another.
    /// </summary>
    Task<TestJob> CreateJobAsync(Data.ObiconDbContext db, Models.TestJob job);
    Task<TestJob?> DequeueTestAsync(Guid nodeId);

    /// <summary>
    /// Updates a job's status and applies the TestResultStorageMode to terminal
    /// statuses: Full keeps the payload, MetadataOnly strips it, None deletes the row.
    /// Returns the job as of the update - including when the row was deleted - so
    /// metrics are emitted before deletion and never lost.
    /// </summary>
    Task<TestJob?> UpdateJobStatusAsync(Guid jobId, TestJobStatus status, TestResult? result = null, string? errorMessage = null);
    Task<TestJob?> GetJobAsync(Guid jobId);
    Task<Page<TestJob>> GetRunsAsync(TestRunQuery query);
    Task<IEnumerable<TestJob>> GetPendingJobsAsync();
    Task<IEnumerable<TestJob>> GetActiveJobsAsync();
    Task<int> GetQueueLengthAsync();
    Task<Dictionary<TestJobStatus, int>> GetStatusCountsAsync();
    Task MarkJobStartedAsync(Guid jobId);
    Task MarkJobAcknowledgedAsync(Guid jobId);
    Task MarkJobAssignedAsync(Guid jobId);
    Task<List<TestCurrentResult>> GetCurrentResultsAsync();

    /// <summary>
    /// Deletes finished jobs completed before the first cutoff and jobs stuck in a
    /// non-terminal status since before the second cutoff. Returns the number of
    /// deleted rows; live jobs are never touched.
    /// </summary>
    Task<int> PruneJobsAsync(DateTime finishedBefore, DateTime stuckBefore);
}

/// <summary>
/// The latest job status of a created test; <see cref="LatestStatus"/> is null when the test never ran.
/// </summary>
public record TestCurrentResult(Guid TestId, string TestName, TestJobStatus? LatestStatus);
