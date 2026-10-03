using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

/// <summary>
/// Stores and transitions test jobs. Terminal statuses apply the TestResultStorageMode:
/// Full keeps the payload, MetadataOnly strips it, None deletes the row - always returning
/// the job to the caller first, so metrics are emitted before any deletion.
/// </summary>
public class TestQueueService : ITestQueueService
{
    private static readonly TestJobStatus[] TerminalStatuses =
    [
        TestJobStatus.Completed, TestJobStatus.Failed, TestJobStatus.Timeout, TestJobStatus.NoRun
    ];

    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly IServerSettingsService _settingsService;

    public TestQueueService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        IServerSettingsService settingsService)
    {
        _dbFactory = dbFactory;
        _settingsService = settingsService;
    }

    public async Task<TestJob> EnqueueJobAsync(TestJob job)
    {
        return await _dbFactory.ExecuteAsync(db => CreateJobAsync(db, job));
    }

    /// <summary>
    /// Creates the job directly with the given context, clamping its timeout to the
    /// server's MaxTestTimeoutSeconds, for callers that already hold a context for
    /// their own unit of work.
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
        return _dbFactory.ExecuteAsync(async db =>
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

    public async Task<TestJob?> UpdateJobStatusAsync(Guid jobId, TestJobStatus status, TestResult? result = null, string? errorMessage = null)
    {
        return await _dbFactory.ExecuteAsync(async db =>
        {
            var job = await db.TestJobs.FindAsync(jobId);
            if (job == null)
            {
                return null;
            }

            var terminal = TerminalStatuses.Contains(status);
            job.Status = status;

            if (status == TestJobStatus.Running)
            {
                job.StartedAt = DateTime.UtcNow;
            }

            if (terminal)
            {
                job.CompletedAt = job.CompletedAt ?? DateTime.UtcNow;
            }

            var mode = await GetStorageModeAsync();
            if (terminal && mode == TestResultStorageMode.None)
            {
                // Delete the row, but hand the in-memory job back: the caller still
                // emits the run's metrics, which must never be lost to storage policy
                db.TestJobs.Remove(job);
                await db.SaveChangesAsync();
                return job;
            }

            // MetadataOnly keeps the row skeleton and error message; only the payload goes
            job.Result = terminal && mode == TestResultStorageMode.MetadataOnly ? null : result;
            job.ErrorMessage = errorMessage;

            await db.SaveChangesAsync();
            return job;
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
        return _dbFactory.ExecuteAsync(async db =>
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
        return _dbFactory.ExecuteAsync(async db =>
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
        return _dbFactory.ExecuteAsync(async db =>
        {
            var job = await db.TestJobs.FindAsync(jobId);
            if (job != null)
            {
                job.Status = TestJobStatus.Assigned;
                await db.SaveChangesAsync();
            }
        });
    }

    public async Task<int> PruneJobsAsync(DateTime finishedBefore, DateTime stuckBefore)
    {
        return await _dbFactory.ExecuteAsync(async db =>
        {
            var finished = await db.TestJobs
                .Where(j => TerminalStatuses.Contains(j.Status) && (j.CompletedAt ?? j.CreatedAt) < finishedBefore)
                .ExecuteDeleteAsync();

            // Safety cap: a job stuck in a live status for far longer than any test
            // timeout can never finish; it would pin disk forever without this sweep
            var stuck = await db.TestJobs
                .Where(j => !TerminalStatuses.Contains(j.Status) && j.CreatedAt < stuckBefore)
                .ExecuteDeleteAsync();

            return finished + stuck;
        });
    }

    /// <summary>
    /// The effective TestResultStorageMode setting, defaulting to Full on unknown values.
    /// </summary>
    private async Task<TestResultStorageMode> GetStorageModeAsync()
    {
        var raw = await _settingsService.GetAsync<string>("TestResultStorageMode");
        return Enum.TryParse(raw, out TestResultStorageMode mode) ? mode : TestResultStorageMode.Full;
    }
}
