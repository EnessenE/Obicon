using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;
using Obicon.Server.WebSockets;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Services;

public partial class TestService : ITestService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly SqliteWriteQueue _writeQueue;
    private readonly INodeService _nodeService;
    private readonly INodePoolService _poolService;
    private readonly ITestQueueService _queueService;
    private readonly IServerSettingsService _settingsService;
    private readonly NodeConnectionManager _connectionManager;
    private readonly ILogger<TestService> _logger;

    public TestService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        SqliteWriteQueue writeQueue,
        INodeService nodeService,
        INodePoolService poolService,
        ITestQueueService queueService,
        IServerSettingsService settingsService,
        NodeConnectionManager connectionManager,
        ILogger<TestService> logger)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _writeQueue = writeQueue;
        _nodeService = nodeService;
        _poolService = poolService;
        _queueService = queueService;
        _settingsService = settingsService;
        _connectionManager = connectionManager;
    }

    public async Task<TestResponse> CreateTestAsync(CreateTestRequest request)
    {
        await ValidateTargetsAsync(request.NodeIds, request.PoolIds);
        await ValidateFrequencyAsync(request.Frequency);
        ValidateHttpExpectations(request.Type, request.ExpectedBodyPattern, request.Headers, request.ProxyUrl);

        var test = new Test
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Target = request.Target,
            NodeIds = request.NodeIds,
            PoolIds = request.PoolIds,
            Frequency = request.Frequency,
            IsActive = request.IsActive,
            ExpectedStatusCodes = request.ExpectedStatusCodes,
            CheckCertificateExpiryDays = request.CheckCertificateExpiryDays,
            ExpectedDnsResult = request.ExpectedDnsResult,
            IpVersion = request.IpVersion,
            TimeoutSeconds = request.TimeoutSeconds,
            ExpectedBodyPattern = request.ExpectedBodyPattern,
            Headers = request.Headers ?? new Dictionary<string, string>(),
            ProxyUrl = request.ProxyUrl,
            CacheBust = request.CacheBust,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        await _writeQueue.EnqueueAsync(async db =>
        {
            db.Tests.Add(test);
            await db.SaveChangesAsync();
        });

        LogCreatedTest(test.Id, test.Name, test.Type, test.Target);
        Metrics.ServerMetrics.Action("created_test");
        return ToResponse(test);
    }

    public async Task<IEnumerable<TestResponse>> GetAllTestsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var tests = await db.Tests.OrderBy(t => t.CreatedAt).ToListAsync();
        return tests.Select(ToResponse);
    }

    public async Task<TestResponse?> GetTestAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var test = await db.Tests.FindAsync(id);
        return test == null ? null : ToResponse(test);
    }

    public async Task<TestResponse?> UpdateTestAsync(Guid id, UpdateTestRequest request)
    {
        await ValidateTargetsAsync(request.NodeIds, request.PoolIds);
        await ValidateFrequencyAsync(request.Frequency);
        ValidateHttpExpectations(request.Type, request.ExpectedBodyPattern, request.Headers, request.ProxyUrl);

        var updated = await _writeQueue.EnqueueAsync(async db =>
        {
            var test = await db.Tests.FindAsync(id);
            if (test == null)
            {
                return (Test?)null;
            }

            test.Type = request.Type;
            test.Target = request.Target;
            test.NodeIds = request.NodeIds;
            test.PoolIds = request.PoolIds;
            test.Frequency = request.Frequency;
            test.IsActive = request.IsActive;
            test.ExpectedStatusCodes = request.ExpectedStatusCodes;
            test.CheckCertificateExpiryDays = request.CheckCertificateExpiryDays;
            test.ExpectedDnsResult = request.ExpectedDnsResult;
            test.IpVersion = request.IpVersion;
            test.TimeoutSeconds = request.TimeoutSeconds;
            test.ExpectedBodyPattern = request.ExpectedBodyPattern;
            test.Headers = request.Headers ?? new Dictionary<string, string>();
            test.ProxyUrl = request.ProxyUrl;
            test.CacheBust = request.CacheBust;
            test.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return test;
        });

        return updated == null ? null : ToResponse(updated);
    }

    public async Task<TestResponse?> ToggleTestAsync(Guid id)
    {
        var toggled = await _writeQueue.EnqueueAsync(async db =>
        {
            var test = await db.Tests.FindAsync(id);
            if (test == null)
            {
                return (Test?)null;
            }

            test.IsActive = !test.IsActive;
            test.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            LogTestToggled(test.Id, test.Name, test.IsActive ? "active" : "inactive");
            return test;
        });

        return toggled == null ? null : ToResponse(toggled);
    }

    public async Task<bool> DeleteTestAsync(Guid id)
    {
        var deleted = await _writeQueue.EnqueueAsync(async db =>
        {
            var test = await db.Tests.FindAsync(id);
            if (test == null)
            {
                return false;
            }

            db.Tests.Remove(test);
            await db.SaveChangesAsync();

            LogDeletedTest(test.Id, test.Name);
            Metrics.ServerMetrics.Action("deleted_test");
            return true;
        });
        return deleted;
    }

    public async Task<bool> TriggerTestRunAsync(Guid testId)
    {
        return await _writeQueue.EnqueueAsync(async db =>
        {
            var test = await db.Tests.FirstOrDefaultAsync(t => t.Id == testId && t.IsActive);
            if (test == null)
            {
                return false;
            }

            await EnqueueJobsForTestAsync(db, test);
            LogManualRunTriggered(test.Id, test.Name);
            return true;
        });
    }

    /// <summary>
    /// Enqueues jobs for every active test whose frequency interval has elapsed.
    /// Called by the scheduler; overdue tests run once and resynchronize instead of catching up.
    /// </summary>
    public async Task<int> ScheduleDueTestsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var activeTests = await db.Tests.Where(t => t.IsActive).ToListAsync();

        var scheduled = 0;
        foreach (var due in activeTests)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, due.Frequency));
            var anchor = due.LastScheduledAt ?? due.CreatedAt;
            if (now - anchor < interval)
            {
                continue;
            }

            // One queued unit per due test: re-check and persist LastScheduledAt before
            // enqueuing, so a row that cannot be updated does not create jobs either
            var enqueued = await _writeQueue.EnqueueAsync(async db =>
            {
                var test = await db.Tests.FindAsync(due.Id);
                if (test == null || !test.IsActive)
                {
                    return false;
                }

                var currentAnchor = test.LastScheduledAt ?? test.CreatedAt;
                if (DateTime.UtcNow - currentAnchor < TimeSpan.FromSeconds(Math.Max(1, test.Frequency)))
                {
                    return false; // another run happened while this unit waited
                }

                test.LastScheduledAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                await EnqueueJobsForTestAsync(db, test);
                LogSchedulerEnqueuedTest(test.Id, test.Name);
                return true;
            });

            if (enqueued)
            {
                scheduled++;
            }
        }

        return scheduled;
    }

    /// <summary>
    /// Parses the FrequencyPresetsSeconds setting into a sorted list of positive second values.
    /// Falls back to the default presets when the stored value is unusable.
    /// </summary>
    private async Task<List<int>> GetFrequencyPresetsAsync()
    {
        var raw = await _settingsService.GetAsync<string>("FrequencyPresetsSeconds");
        return FrequencyPresets.Parse(raw);
    }

    private async Task ValidateFrequencyAsync(int frequency)
    {
        var presets = await GetFrequencyPresetsAsync();
        if (!presets.Contains(frequency))
        {
            throw new ArgumentException($"Frequency must be one of the configured presets (in seconds): {string.Join(", ", presets)}");
        }
    }

    /// <summary>
    /// Validates HTTP/HTTPS expectations: the body regex must compile, headers must have
    /// usable names, and the proxy must be an absolute http(s) URL. Non-HTTP types get the
    /// fields ignored (empty), so stale values cannot leak into e.g. a DNS test.
    /// </summary>
    private static void ValidateHttpExpectations(
        Shared.Models.Enums.TestType type,
        string? expectedBodyPattern,
        Dictionary<string, string>? headers,
        string? proxyUrl)
    {
        var isHttp = type is Shared.Models.Enums.TestType.Http or Shared.Models.Enums.TestType.Https;
        if (!isHttp)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(expectedBodyPattern))
        {
            try
            {
                _ = new Regex(expectedBodyPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Expected body pattern is not a valid regular expression: {ex.Message}");
            }
        }

        if (headers != null)
        {
            foreach (var header in headers)
            {
                var name = header.Key?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Header names must not be empty");
                }
                if (name.IndexOfAny([' ', '\t', '\r', '\n', ':']) >= 0)
                {
                    throw new ArgumentException($"Header name '{name}' contains invalid characters (whitespace or colon)");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxy) ||
                (proxy.Scheme != "http" && proxy.Scheme != "https"))
            {
                throw new ArgumentException("Proxy must be an absolute http:// or https:// URL");
            }
        }
    }

    /// <summary>
    /// Creates the test's jobs with the given context. Only called from inside queued
    /// write units: the job creation must not re-enter the queue (deadlock), and the
    /// single consumer is already the only writer.
    /// </summary>
    private async Task EnqueueJobsForTestAsync(ObiconDbContext db, Test test)
    {
        // One job per targeted node: direct node IDs plus all pool members, deduplicated
        foreach (var nodeId in await ResolveTargetNodesAsync(test))
        {
            await _queueService.CreateJobAsync(db, new TestJob
            {
                TestId = test.Id,
                NodeId = nodeId,
                TestType = test.Type,
                Target = test.Target,
                TimeoutSeconds = test.TimeoutSeconds,
                ExpectedStatusCodes = test.ExpectedStatusCodes,
                CheckCertificateExpiryDays = test.CheckCertificateExpiryDays,
                ExpectedDnsResult = test.ExpectedDnsResult,
                IpVersion = test.IpVersion,
                ExpectedBodyPattern = test.ExpectedBodyPattern,
                Headers = test.Headers,
                ProxyUrl = test.ProxyUrl,
                CacheBust = test.CacheBust
            });
        }
    }

    /// <summary>
    /// Runs a test once on each selected connected node without creating a test first.
    /// Explicit node IDs run directly; pools contribute their top 3 connected members,
    /// least busy first. Validation errors throw ArgumentException for a 400 response.
    /// </summary>
    public async Task<List<Models.TestJob>> RunOnceAsync(Models.Requests.RunTestOnceRequest request)
    {
        ValidateHttpExpectations(request.Type, request.ExpectedBodyPattern, request.Headers, request.ProxyUrl);

        if (request.NodeIds.Count == 0 && request.PoolIds.Count == 0)
        {
            throw new ArgumentException("At least one node ID or pool ID is required for a run-once");
        }

        // One queued write unit: node and pool existence, selection, and job creation
        // happen together, so the selection sees a consistent database state and the
        // created jobs are written by the single consumer
        var jobs = await _writeQueue.EnqueueAsync(async db =>
        {
            // Directly selected nodes must all exist
            var nodes = await db.Nodes
                .Where(n => request.NodeIds.Distinct().Contains(n.Id))
                .ToListAsync();
            var missing = request.NodeIds.Distinct().Except(nodes.Select(n => n.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new ArgumentException($"Unknown node ID: {missing[0]}");
            }
            var selectedNodeIds = nodes.Select(n => n.Id).ToList();

            // Pools contribute their members; the top 3 connected ones are picked later.
            // Ordered list, so ties in busyness keep a stable selection order
            var poolCandidates = new List<Guid>();
            foreach (var poolId in request.PoolIds.Distinct())
            {
                var pool = await db.NodePools.FindAsync(poolId)
                    ?? throw new ArgumentException($"Unknown pool ID: {poolId}");
                foreach (var memberId in pool.NodeIds.Where(id => !selectedNodeIds.Contains(id) && !poolCandidates.Contains(id)))
                {
                    poolCandidates.Add(memberId);
                }
            }

            var connectedIds = _connectionManager.GetAllConnections()
                .Select(c => Guid.TryParse(c.NodeId, out var id) ? id : (Guid?)null)
                .Where(id => id != null)
                .Select(id => id!.Value)
                .ToHashSet();

            var activeJobsPerNode = (await db.TestJobs.ToListAsync())
                .Where(j => j.Status is TestJobStatus.Queued or TestJobStatus.Assigned or TestJobStatus.Running)
                .GroupBy(j => j.NodeId)
                .ToDictionary(g => g.Key, g => g.Count());

            selectedNodeIds.AddRange(SelectTopNodes(poolCandidates, connectedIds, activeJobsPerNode, PoolSelectionLimit));

            // Jobs only go to connected nodes; at least one must be connected
            selectedNodeIds = selectedNodeIds.Where(id => connectedIds.Contains(id)).Distinct().ToList();
            if (selectedNodeIds.Count == 0)
            {
                throw new ArgumentException("None of the selected nodes are connected");
            }

            var created = new List<Models.TestJob>();
            foreach (var nodeId in selectedNodeIds)
            {
                created.Add(await _queueService.CreateJobAsync(db, new TestJob
                {
                    TestId = Guid.Empty,
                    NodeId = nodeId,
                    TestType = request.Type,
                    Target = request.Target,
                    TimeoutSeconds = request.TimeoutSeconds ?? 60,
                    ExpectedStatusCodes = request.ExpectedStatusCodes,
                    CheckCertificateExpiryDays = request.CheckCertificateExpiryDays,
                    ExpectedDnsResult = request.ExpectedDnsResult,
                    IpVersion = request.IpVersion,
                    ExpectedBodyPattern = request.ExpectedBodyPattern,
                    Headers = request.Headers ?? new Dictionary<string, string>(),
                    ProxyUrl = request.ProxyUrl,
                    CacheBust = request.CacheBust
                }));
            }
            return created;
        });

        LogRunOnceEnqueued(jobs.Count, request.Type, request.Target);
        return jobs;
    }

    /// <summary>
    /// How many connected pool members a run-once selects, ordered by least busy first.
    /// </summary>
    public const int PoolSelectionLimit = 3;

    /// <summary>
    /// Selects the top connected pool candidates for a run-once: connected nodes only,
    /// ordered by fewest active jobs (ties keep the input order), limited to the limit.
    /// </summary>
    internal static List<Guid> SelectTopNodes(
        IEnumerable<Guid> candidates,
        HashSet<Guid> connectedNodeIds,
        Dictionary<Guid, int> activeJobsPerNode,
        int limit)
    {
        return candidates
            .Where(id => connectedNodeIds.Contains(id))
            .OrderBy(id => activeJobsPerNode.TryGetValue(id, out var count) ? count : 0)
            .Take(limit)
            .ToList();
    }

    public async Task<IEnumerable<Test>> GetTestsForNodeAsync(Guid nodeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var tests = await db.Tests
            .Where(t => t.IsActive)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        return tests.Where(t => t.NodeIds.Contains(nodeId));
    }

    private async Task ValidateTargetsAsync(List<Guid> nodeIds, List<Guid> poolIds)
    {
        if (nodeIds.Count == 0 && poolIds.Count == 0)
        {
            throw new ArgumentException("A test needs at least one node or pool");
        }

        foreach (var nodeId in nodeIds)
        {
            if (await _nodeService.GetNodeAsync(nodeId) == null)
            {
                throw new ArgumentException("Invalid node ID: " + nodeId);
            }
        }

        foreach (var poolId in poolIds)
        {
            if (await _poolService.GetPoolAsync(poolId) == null)
            {
                throw new ArgumentException("Invalid pool ID: " + poolId);
            }
        }
    }

    private async Task<List<Guid>> ResolveTargetNodesAsync(Test test)
    {
        var nodeIds = new HashSet<Guid>(test.NodeIds);
        foreach (var poolId in test.PoolIds)
        {
            var pool = await _poolService.GetPoolAsync(poolId);
            if (pool != null)
            {
                foreach (var nodeId in pool.NodeIds)
                {
                    nodeIds.Add(nodeId);
                }
            }
        }
        return nodeIds.ToList();
    }

    private static TestResponse ToResponse(Test test) => new()
    {
        Id = test.Id,
        Name = test.Name,
        Type = test.Type,
        Target = test.Target,
        NodeIds = test.NodeIds,
        PoolIds = test.PoolIds,
        Frequency = test.Frequency,
        IsActive = test.IsActive,
        ExpectedStatusCodes = test.ExpectedStatusCodes,
        CheckCertificateExpiryDays = test.CheckCertificateExpiryDays,
        ExpectedDnsResult = test.ExpectedDnsResult,
        IpVersion = test.IpVersion,
        TimeoutSeconds = test.TimeoutSeconds,
        ExpectedBodyPattern = test.ExpectedBodyPattern,
        Headers = test.Headers,
        ProxyUrl = test.ProxyUrl,
        CacheBust = test.CacheBust,
        CreatedAt = test.CreatedAt,
        UpdatedAt = test.UpdatedAt
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Created test {TestId} ({TestName}, type {TestType}, target {Target})")]
    private partial void LogCreatedTest(Guid testId, string testName, TestType testType, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "Test {TestId} ({TestName}) is now {State}")]
    private partial void LogTestToggled(Guid testId, string testName, string state);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted test {TestId} ({TestName})")]
    private partial void LogDeletedTest(Guid testId, string testName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Manual run triggered for test {TestId} ({TestName})")]
    private partial void LogManualRunTriggered(Guid testId, string testName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduler enqueued test {TestId} ({TestName})")]
    private partial void LogSchedulerEnqueuedTest(Guid testId, string testName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run-once enqueued {Count} job(s) on selected nodes ({TestType} {Target})")]
    private partial void LogRunOnceEnqueued(int count, TestType testType, string target);
}
