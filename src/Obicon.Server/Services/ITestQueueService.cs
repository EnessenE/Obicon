using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

public interface ITestQueueService
{
    Task<TestJob> EnqueueJobAsync(Models.TestJob job);

    /// <summary>
    /// Creates a job directly with the given context. Only for code already running
    /// inside a queued write unit; everything else goes through EnqueueJobAsync.
    /// </summary>
    Task<TestJob> CreateJobAsync(Data.ObiconDbContext db, Models.TestJob job);
    Task<TestJob?> DequeueTestAsync(Guid nodeId);
    Task UpdateJobStatusAsync(Guid jobId, TestJobStatus status, TestResult? result = null, string? errorMessage = null);
    Task<TestJob?> GetJobAsync(Guid jobId);
    Task<IEnumerable<TestJob>> GetJobsForNodeAsync(Guid nodeId);
    Task<IEnumerable<TestJob>> GetAllJobsAsync();
    Task<IEnumerable<TestJob>> GetPendingJobsAsync();
    Task<IEnumerable<TestJob>> GetActiveJobsAsync();
    Task<int> GetQueueLengthAsync();
    Task<Dictionary<TestJobStatus, int>> GetStatusCountsAsync();
    Task MarkJobStartedAsync(Guid jobId);
    Task MarkJobAcknowledgedAsync(Guid jobId);
    Task MarkJobAssignedAsync(Guid jobId);
    Task<List<TestCurrentResult>> GetCurrentResultsAsync();
}

/// <summary>
/// The latest job status of a created test; <see cref="LatestStatus"/> is null when the test never ran.
/// </summary>
public record TestCurrentResult(Guid TestId, string TestName, TestJobStatus? LatestStatus);
