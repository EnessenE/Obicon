using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

public class TestQueueService : ITestQueueService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly ServerSettings _settings;

    public TestQueueService(IDbContextFactory<ObiconDbContext> dbFactory, IOptions<ServerSettings> settings)
    {
        _dbFactory = dbFactory;
        _settings = settings.Value;
    }

    public async Task<TestJob> EnqueueTestAsync(Guid testId, Guid nodeId, TestType testType, string target, int? timeoutSeconds = null)
    {
        var job = new TestJob
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            NodeId = nodeId,
            TestType = testType,
            Target = target,
            TimeoutSeconds = Math.Clamp(timeoutSeconds ?? _settings.MaxTestTimeoutSeconds, 1, Math.Max(1, _settings.MaxTestTimeoutSeconds)),
            Status = TestJobStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.TestJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    }

    public async Task<TestJob?> DequeueTestAsync(Guid nodeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var job = await db.TestJobs
            .Where(j => j.NodeId == nodeId && j.Status == TestJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync();

        if (job != null)
        {
            job.Status = TestJobStatus.Assigned;
            await db.SaveChangesAsync();
        }

        return job;
    }

    public async Task UpdateJobStatusAsync(Guid jobId, TestJobStatus status, TestResult? result = null, string? errorMessage = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var job = await db.TestJobs.FindAsync(jobId);
        if (job == null)
        {
            return;
        }

        job.Status = status;
        job.Result = result;
        job.ErrorMessage = errorMessage;

        if (status == TestJobStatus.Running)
        {
            job.StartedAt = DateTime.UtcNow;
        }

        if (status is TestJobStatus.Completed or TestJobStatus.Failed or TestJobStatus.Timeout)
        {
            job.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task<TestJob?> GetJobAsync(Guid jobId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TestJobs.FindAsync(jobId);
    }

    public async Task<IEnumerable<TestJob>> GetJobsForNodeAsync(Guid nodeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TestJobs
            .Where(j => j.NodeId == nodeId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestJob>> GetAllJobsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TestJobs
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestJob>> GetPendingJobsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TestJobs
            .Where(j => j.Status == TestJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestJob>> GetActiveJobsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TestJobs
            .Where(j => j.Status == TestJobStatus.Assigned || j.Status == TestJobStatus.Running)
            .ToListAsync();
    }

    public async Task<int> GetQueueLengthAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TestJobs.CountAsync(j => j.Status == TestJobStatus.Queued);
    }

    public async Task MarkJobStartedAsync(Guid jobId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var job = await db.TestJobs.FindAsync(jobId);
        if (job != null)
        {
            job.Status = TestJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task MarkJobAssignedAsync(Guid jobId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var job = await db.TestJobs.FindAsync(jobId);
        if (job != null)
        {
            job.Status = TestJobStatus.Assigned;
            await db.SaveChangesAsync();
        }
    }
}
