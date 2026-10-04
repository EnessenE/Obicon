using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

/// <summary>
/// Stores and transitions test jobs. Terminal statuses apply the TestResultStorageMode:
/// Full keeps the payload, MetadataOnly strips the structured details but keeps the
/// scalar outcome, None deletes the row - always returning the job to the caller first,
/// so metrics are emitted before any deletion.
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

    /// <summary>
    /// Loads jobs with their header rows and every details section, for building API
    /// responses. Split queries, so the collection includes never form one wide
    /// cartesian join (its inflated plan cost would push postgres over its JIT
    /// threshold and cost hundreds of milliseconds of compilation per execution),
    /// and no tracking, because response mapping never mutates the entities.
    /// </summary>
    private static IQueryable<TestJob> WithFullLoad(ObiconDbContext db) => db.TestJobs
        .AsNoTracking()
        .AsSplitQuery()
        .Include(j => j.Headers)
        .Include(j => j.Traceroute!).ThenInclude(d => d.Hops!)
        .ThenInclude(h => h.Probes)
        .Include(j => j.Ping!).ThenInclude(d => d.Replies)
        .Include(j => j.Tcp)
        .Include(j => j.Http)
        .Include(j => j.Dns!).ThenInclude(d => d.Records)
        .Include(j => j.Tls)
        .Include(j => j.Certificate);

    /// <summary>
    /// Loads jobs with just their header rows, for dispatch and transitions; the
    /// structured details of a queued or running job are always empty.
    /// </summary>
    private static IQueryable<TestJob> WithDispatchLoad(ObiconDbContext db) => db.TestJobs
        .Include(j => j.Headers);

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
            var job = await WithDispatchLoad(db)
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
                // Delete the row (the cascading foreign keys take the header and details
                // rows with it), but hand the in-memory job back: the caller still emits
                // the run's metrics, which must never be lost to storage policy
                db.TestJobs.Remove(job);
                await db.SaveChangesAsync();
                return job;
            }

            if (result != null)
            {
                job.Success = result.Success;
                job.DurationMs = result.DurationMs;
                job.Output = result.Output;

                // MetadataOnly keeps the scalar outcome but strips the structured
                // payload: no detail rows are written for the run
                if (result.Details != null && (!terminal || mode == TestResultStorageMode.Full))
                {
                    TestResultDetailsMapper.Persist(db, job.Id, result.Details);
                }
            }
            job.ErrorMessage = errorMessage;

            await db.SaveChangesAsync();
            return job;
        });
    }

    public async Task<TestJob?> GetJobAsync(Guid jobId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await WithFullLoad(db).FirstOrDefaultAsync(j => j.Id == jobId);
    }

    /// <summary>
    /// One page of test runs, newest first, with the server-side filters of the query
    /// applied and the total number of matching runs across all pages. The page window
    /// is applied before the details load, so a page never loads more than its jobs.
    /// </summary>
    public async Task<Page<TestJob>> GetRunsAsync(TestRunQuery query)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var items = await SortRuns(ApplyRunFilters(WithFullLoad(db), query), query)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync();

        var total = await ApplyRunFilters(db.TestJobs, query).CountAsync();

        return new Page<TestJob>(items, total, query.Limit, query.Offset);
    }

    /// <summary>
    /// Applies the query's sort to the filtered runs: createdAt, durationMs, or
    /// status, each ascending or descending. Null durations sort before measured
    /// durations so in-flight runs land next to each other instead of scattered.
    /// </summary>
    private static IOrderedQueryable<TestJob> SortRuns(IQueryable<TestJob> jobs, TestRunQuery query)
    {
        return (query.SortBy, query.Descending) switch
        {
            (TestRunSortBy.DurationMs, false) => jobs.OrderBy(j => j.DurationMs),
            (TestRunSortBy.DurationMs, true) => jobs.OrderByDescending(j => j.DurationMs),
            (TestRunSortBy.Status, false) => jobs.OrderBy(j => j.Status),
            (TestRunSortBy.Status, true) => jobs.OrderByDescending(j => j.Status),
            (_, false) => jobs.OrderBy(j => j.CreatedAt),
            (_, true) => jobs.OrderByDescending(j => j.CreatedAt)
        };
    }

    /// <summary>
    /// The filters shared by the page query and the total count: status, node, test,
    /// and a case-insensitive search over target, output, and error message.
    /// </summary>
    private static IQueryable<TestJob> ApplyRunFilters(IQueryable<TestJob> jobs, TestRunQuery query)
    {
        if (query.Status != null)
        {
            jobs = jobs.Where(j => j.Status == query.Status);
        }
        if (query.NodeId != null)
        {
            jobs = jobs.Where(j => j.NodeId == query.NodeId);
        }
        if (query.TestId != null)
        {
            jobs = jobs.Where(j => j.TestId == query.TestId);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            jobs = jobs.Where(j =>
                EF.Functions.ILike(j.Target, pattern) ||
                (j.Output != null && EF.Functions.ILike(j.Output, pattern)) ||
                (j.ErrorMessage != null && EF.Functions.ILike(j.ErrorMessage, pattern)));
        }

        return jobs;
    }

    public async Task<IEnumerable<TestJob>> GetPendingJobsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await WithDispatchLoad(db)
            .Where(j => j.Status == TestJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestJob>> GetActiveJobsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await WithDispatchLoad(db)
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
