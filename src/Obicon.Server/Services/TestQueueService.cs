using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

public class TestQueueService : ITestQueueService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly SqliteWriteQueue _writeQueue;
    private readonly IServerSettingsService _settingsService;

    public TestQueueService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        SqliteWriteQueue writeQueue,
        IServerSettingsService settingsService)
    {
        _dbFactory = dbFactory;
        _writeQueue = writeQueue;
        _settingsService = settingsService;
    }

    public async Task<TestJob> EnqueueJobAsync(TestJob job)
    {
        return await _writeQueue.EnqueueAsync(db => CreateJobAsync(db, job));
    }

    /// <summary>
    /// Creates the job directly with the given context, clamping its timeout to the
    /// server's MaxTestTimeoutSeconds. Used by code that already runs inside a queued
    /// write unit: going through the queue again would deadlock, and the single
    /// consumer is already the only writer.
    /// </summary>
    public async Task<TestJob> CreateJobAsync(ObiconDbContext db, TestJob job)
    {
        job.TimeoutSeconds = Math.Clamp(job.TimeoutSeconds, 1, Math.Max(1, await _settingsService.GetAsync<int>("MaxTestTimeoutSeconds")));
        job.Id = job.Id == Guid.Empty ? Guid.NewGuid() : job.Id;
        job.CreatedAt = job.CreatedAt == default ? DateTime.UtcNow : job.CreatedAt;
        job.Status = TestJobStatus.Queued;

        db.TestJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    }

    public Task<TestJob?> DequeueTestAsync(Guid nodeId)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
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
        });
    }

    public Task UpdateJobStatusAsync(Guid jobId, TestJobStatus status, TestResult? result = null, string? errorMessage = null)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
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
        });
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

    public async Task<Dictionary<TestJobStatus, int>> GetStatusCountsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TestJobs
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }

    public async Task<List<TestCurrentResult>> GetCurrentResultsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Tests
            .Select(t => new TestCurrentResult(
                t.Id,
                t.Name,
                db.TestJobs
                    .Where(j => j.TestId == t.Id)
                    .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                    .Select(j => (TestJobStatus?)j.Status)
                    .FirstOrDefault()))
            .ToListAsync();
    }

    public Task MarkJobAcknowledgedAsync(Guid jobId)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
            var job = await db.TestJobs.FindAsync(jobId);
            if (job != null && job.AcknowledgedAt == null)
            {
                job.AcknowledgedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        });
    }

    public Task MarkJobStartedAsync(Guid jobId)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
            var job = await db.TestJobs.FindAsync(jobId);
            if (job != null)
            {
                job.Status = TestJobStatus.Running;
                job.StartedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        });
    }

    public Task MarkJobAssignedAsync(Guid jobId)
    {
        return _writeQueue.EnqueueAsync(async db =>
        {
            var job = await db.TestJobs.FindAsync(jobId);
            if (job != null)
            {
                job.Status = TestJobStatus.Assigned;
                await db.SaveChangesAsync();
            }
        });
    }
}
