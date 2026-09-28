using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

public class TestQueueService : ITestQueueService
{
    private readonly List<TestJob> _jobs = new();
    private readonly object _lock = new();

    public Task<TestJob> EnqueueTestAsync(Guid testId, Guid nodeId)
    {
        var job = new TestJob
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            NodeId = nodeId,
            Status = TestJobStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            _jobs.Add(job);
        }

        return Task.FromResult(job);
    }

    public Task<TestJob?> DequeueTestAsync(Guid nodeId)
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j =>
                j.NodeId == nodeId &&
                j.Status == TestJobStatus.Queued);
            
            if (job != null)
            {
                job.Status = TestJobStatus.Assigned;
            }
            return Task.FromResult<TestJob?>(job);
        }
    }

    public Task UpdateJobStatusAsync(Guid jobId, TestJobStatus status, TestResult? result = null, string? errorMessage = null)
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job != null)
            {
                job.Status = status;
                job.Result = result;
                job.ErrorMessage = errorMessage;
                job.CompletedAt = DateTime.UtcNow;

                if (status == TestJobStatus.Running)
                {
                    job.StartedAt = DateTime.UtcNow;
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task<TestJob?> GetJobAsync(Guid jobId)
    {
        lock (_lock)
        {
            return Task.FromResult<TestJob?>(_jobs.FirstOrDefault(j => j.Id == jobId));
        }
    }

    public Task<IEnumerable<TestJob>> GetJobsForNodeAsync(Guid nodeId)
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<TestJob>>(
                _jobs.Where(j => j.NodeId == nodeId).ToList());
        }
    }

    public Task<int> GetQueueLengthAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_jobs.Count(j => j.Status == TestJobStatus.Queued));
        }
    }

    public Task MarkJobStartedAsync(Guid jobId)
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job != null)
            {
                job.Status = TestJobStatus.Running;
                job.StartedAt = DateTime.UtcNow;
            }
        }
        return Task.CompletedTask;
    }
}
