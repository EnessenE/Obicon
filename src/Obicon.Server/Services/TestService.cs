using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.WebSockets;
using Obicon.Server.Models;
using Obicon.Shared.Models.Enums;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class TestService : ITestService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly INodeService _nodeService;
    private readonly INodePoolService _poolService;
    private readonly ITestQueueService _queueService;
    private readonly IServerSettingsService _settingsService;
    private readonly NodeConnectionManager _connectionManager;
    private readonly ILogger<TestService> _logger;

    public TestService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        INodeService nodeService,
        INodePoolService poolService,
        ITestQueueService queueService,
        IServerSettingsService settingsService,
        NodeConnectionManager connectionManager,
        ILogger<TestService> logger)
    {
        _logger = logger;
        _dbFactory = dbFactory;
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Tests.Add(test);
        await db.SaveChangesAsync();

        _logger.LogInformation("Created test {TestId} ({TestName}, type {TestType}, target {Target})", test.Id, test.Name, test.Type, test.Target);
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

        await using var db = await _dbFactory.CreateDbContextAsync();
        var test = await db.Tests.FindAsync(id);
        if (test == null)
        {
            return null;
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
        test.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToResponse(test);
    }

    public async Task<TestResponse?> ToggleTestAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var test = await db.Tests.FindAsync(id);
        if (test == null)
        {
            return null;
        }

        test.IsActive = !test.IsActive;
        test.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        _logger.LogInformation("Test {TestId} ({TestName}) is now {State}", test.Id, test.Name, test.IsActive ? "active" : "inactive");
        return ToResponse(test);
    }

    public async Task<bool> DeleteTestAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var test = await db.Tests.FindAsync(id);
        if (test == null)
        {
            return false;
        }

        db.Tests.Remove(test);
        await db.SaveChangesAsync();

        _logger.LogInformation("Deleted test {TestId} ({TestName})", test.Id, test.Name);
        Metrics.ServerMetrics.Action("deleted_test");
        return true;
    }

    public async Task<bool> TriggerTestRunAsync(Guid testId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var test = await db.Tests.FirstOrDefaultAsync(t => t.Id == testId && t.IsActive);
        if (test == null)
        {
            return false;
        }

        await EnqueueJobsForTestAsync(test);
        _logger.LogInformation("Manual run triggered for test {TestId} ({TestName})", test.Id, test.Name);
        return true;
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
        foreach (var test in activeTests)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, test.Frequency));
            var anchor = test.LastScheduledAt ?? test.CreatedAt;

            if (now - anchor < interval)
            {
                continue;
            }

            // Persist LastScheduledAt before enqueuing: if this row cannot be
            // updated (e.g. externally imported data), the jobs must not be
            // created either or the test would re-enqueue on every tick
            var previous = test.LastScheduledAt;
            test.LastScheduledAt = now;
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                test.LastScheduledAt = previous;
                _logger.LogWarning(ex, "Scheduler could not persist LastScheduledAt for test {TestId} ({TestName}); skipping this cycle", test.Id, test.Name);
                continue;
            }

            await EnqueueJobsForTestAsync(test);
            scheduled++;

            _logger.LogInformation("Scheduler enqueued test {TestId} ({TestName})", test.Id, test.Name);
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
        var presets = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var seconds) ? seconds : 0)
            .Where(seconds => seconds > 0)
            .Distinct()
            .OrderBy(seconds => seconds)
            .ToList();

        return presets.Count > 0
            ? presets
            : ServerSettingDefinitions.All.First(d => d.Key == "FrequencyPresetsSeconds").Default
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToList();
    }

    private async Task ValidateFrequencyAsync(int frequency)
    {
        var presets = await GetFrequencyPresetsAsync();
        if (!presets.Contains(frequency))
        {
            throw new ArgumentException($"Frequency must be one of the configured presets (in seconds): {string.Join(", ", presets)}");
        }
    }

    private async Task EnqueueJobsForTestAsync(Test test)
    {
        // One job per targeted node: direct node IDs plus all pool members, deduplicated
        foreach (var nodeId in await ResolveTargetNodesAsync(test))
        {
            await _queueService.EnqueueJobAsync(new TestJob
            {
                TestId = test.Id,
                NodeId = nodeId,
                TestType = test.Type,
                Target = test.Target,
                TimeoutSeconds = test.TimeoutSeconds,
                ExpectedStatusCodes = test.ExpectedStatusCodes,
                CheckCertificateExpiryDays = test.CheckCertificateExpiryDays,
                ExpectedDnsResult = test.ExpectedDnsResult,
                IpVersion = test.IpVersion
            });
        }
    }

    public async Task<Models.TestJob?> RunOnceAsync(Models.Requests.RunTestOnceRequest request)
    {
        var node = await _nodeService.GetNodeAsync(request.NodeId);
        if (node == null)
        {
            return null;
        }

        if (_connectionManager.GetConnection(request.NodeId.ToString()) == null)
        {
            return null;
        }

        var job = await _queueService.EnqueueJobAsync(new TestJob
        {
            TestId = Guid.Empty,
            NodeId = request.NodeId,
            TestType = request.Type,
            Target = request.Target,
            TimeoutSeconds = request.TimeoutSeconds ?? 60,
            ExpectedStatusCodes = request.ExpectedStatusCodes,
            CheckCertificateExpiryDays = request.CheckCertificateExpiryDays,
            ExpectedDnsResult = request.ExpectedDnsResult,
            IpVersion = request.IpVersion
        });

        _logger.LogInformation("Run-once job {JobId} enqueued on node {NodeId} ({TestType} {Target})", job.Id, request.NodeId, request.Type, request.Target);
        return job;
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
        CreatedAt = test.CreatedAt,
        UpdatedAt = test.UpdatedAt
    };
}
