using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

public interface ITestQueueService
{
    Task<TestJob> EnqueueJobAsync(Models.TestJob job);
    Task<TestJob?> DequeueTestAsync(Guid nodeId);
    Task UpdateJobStatusAsync(Guid jobId, TestJobStatus status, TestResult? result = null, string? errorMessage = null);
    Task<TestJob?> GetJobAsync(Guid jobId);
    Task<IEnumerable<TestJob>> GetJobsForNodeAsync(Guid nodeId);
    Task<IEnumerable<TestJob>> GetAllJobsAsync();
    Task<IEnumerable<TestJob>> GetPendingJobsAsync();
    Task<IEnumerable<TestJob>> GetActiveJobsAsync();
    Task<int> GetQueueLengthAsync();
    Task MarkJobStartedAsync(Guid jobId);
    Task MarkJobAssignedAsync(Guid jobId);
}
