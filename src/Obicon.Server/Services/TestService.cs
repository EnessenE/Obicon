using System.Text.Json;
using System.Text.Json.Serialization;
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
        await ValidateTestTypeEnabledAsync(request.Type);
        ValidateHttpExpectations(request.Type, request.ExpectedBodyPattern, request.Headers, request.ProxyUrl);

        var test = new Test
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Target = request.Target,
            Frequency = request.Frequency,
            IsActive = request.IsActive,
            ExpectedStatusCodes = request.ExpectedStatusCodes,
            CheckCertificateExpiryDays = request.CheckCertificateExpiryDays,
            ExpectedDnsResult = request.ExpectedDnsResult,
            IpVersion = request.IpVersion,
            TimeoutSeconds = request.TimeoutSeconds,
            ExpectedBodyPattern = request.ExpectedBodyPattern,
            ProxyUrl = request.ProxyUrl,
            CacheBust = request.CacheBust,
            TracerouteMaxHops = request.TracerouteMaxHops,
            TracerouteQueriesPerHop = request.TracerouteQueriesPerHop,
            TracerouteQueryTimeoutMs = request.TracerouteQueryTimeoutMs,
            TracerouteResolveHostnames = request.TracerouteResolveHostnames,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
        test.NodeTargets = request.NodeIds.Select(id => new TestTargetNode { TestId = test.Id, NodeId = id }).ToList();
        test.PoolTargets = request.PoolIds.Select(id => new TestTargetPool { TestId = test.Id, PoolId = id }).ToList();
        test.Headers = (request.Headers ?? new Dictionary<string, string>())
            .Select(kv => new TestHeader { TestId = test.Id, Name = kv.Key, Value = kv.Value })
            .ToList();

        await _dbFactory.ExecuteAsync(async db =>
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
        var tests = await db.Tests
            .Include(t => t.NodeTargets)
            .Include(t => t.PoolTargets)
            .Include(t => t.Headers)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        return tests.Select(ToResponse);
    }

    public async Task<TestResponse?> GetTestAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var test = await LoadTestAsync(db, id);
        return test == null ? null : ToResponse(test);
    }

    public async Task<TestResponse?> UpdateTestAsync(Guid id, UpdateTestRequest request)
    {
        await ValidateTargetsAsync(request.NodeIds, request.PoolIds);
        await ValidateFrequencyAsync(request.Frequency);
        await ValidateTestTypeEnabledAsync(request.Type);
        ValidateHttpExpectations(request.Type, request.ExpectedBodyPattern, request.Headers, request.ProxyUrl);

        var updated = await _dbFactory.ExecuteAsync(async db =>
        {
            var test = await LoadTestAsync(db, id);
            if (test == null)
            {
                return (Test?)null;
            }

            test.Type = request.Type;
            test.Target = request.Target;
            test.Frequency = request.Frequency;
            test.IsActive = request.IsActive;
            test.ExpectedStatusCodes = request.ExpectedStatusCodes;
            test.CheckCertificateExpiryDays = request.CheckCertificateExpiryDays;
            test.ExpectedDnsResult = request.ExpectedDnsResult;
            test.IpVersion = request.IpVersion;
            test.TimeoutSeconds = request.TimeoutSeconds;
            test.ExpectedBodyPattern = request.ExpectedBodyPattern;
            test.ProxyUrl = request.ProxyUrl;
            test.CacheBust = request.CacheBust;
            test.TracerouteMaxHops = request.TracerouteMaxHops;
            test.TracerouteQueriesPerHop = request.TracerouteQueriesPerHop;
            test.TracerouteQueryTimeoutMs = request.TracerouteQueryTimeoutMs;
            test.TracerouteResolveHostnames = request.TracerouteResolveHostnames;
            test.PingCount = request.PingCount;
            test.PingTimeoutMs = request.PingTimeoutMs;
            test.PingIntervalMs = request.PingIntervalMs;
            test.HttpMethod = request.HttpMethod;
            test.FollowRedirects = request.FollowRedirects;
            test.DnsNameserver = request.DnsNameserver;
            test.DnsQueryType = request.DnsQueryType;
            test.UpdatedAt = DateTime.UtcNow;

            // Replace the targeting and header rows wholesale: the request carries the
            // complete target set, so the diff is simply old rows out, new rows in
            db.TestTargetNodes.RemoveRange(test.NodeTargets);
            db.TestTargetPools.RemoveRange(test.PoolTargets);
            db.TestHeaders.RemoveRange(test.Headers);
            test.NodeTargets = request.NodeIds.Select(nodeId => new TestTargetNode { TestId = test.Id, NodeId = nodeId }).ToList();
            test.PoolTargets = request.PoolIds.Select(poolId => new TestTargetPool { TestId = test.Id, PoolId = poolId }).ToList();
            test.Headers = (request.Headers ?? new Dictionary<string, string>())
                .Select(kv => new TestHeader { TestId = test.Id, Name = kv.Key, Value = kv.Value })
                .ToList();
            db.TestTargetNodes.AddRange(test.NodeTargets);
            db.TestTargetPools.AddRange(test.PoolTargets);
            db.TestHeaders.AddRange(test.Headers);

            await db.SaveChangesAsync();

            return test;
        });

        return updated == null ? null : ToResponse(updated);
    }

    public async Task<TestResponse?> ToggleTestAsync(Guid id)
    {
        var toggled = await _dbFactory.ExecuteAsync(async db =>
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
        var deleted = await _dbFactory.ExecuteAsync(async db =>
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
        return await _dbFactory.ExecuteAsync(async db =>
        {
            var test = await LoadTestAsync(db, testId);
            if (test == null || !test.IsActive)
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

            // One unit per due test: re-check and persist LastScheduledAt before
            // enqueuing, so a row that cannot be updated does not create jobs either
            var enqueued = await _dbFactory.ExecuteAsync(async db =>
            {
                var test = await LoadTestAsync(db, due.Id);
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
    /// Rejects test types the server has disabled through the EnabledTestTypes setting,
    /// a JSON array of TestType values such as ["Ping","Http","Dns"]. An empty or
    /// missing setting enables every type.
    /// </summary>
    private static readonly JsonSerializerOptions TestTypeListOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc />
    public async Task<List<Models.Responses.TestTypeInfo>> GetTestTypesAsync()
    {
        var enabled = await ResolveEnabledTestTypesAsync();
        return Enum.GetValues<TestType>()
            .Select(type => new Models.Responses.TestTypeInfo
            {
                Type = type,
                Name = type.ToString(),
                Enabled = enabled == null || enabled.Contains(type)
            })
            .ToList();
    }

    private async Task ValidateTestTypeEnabledAsync(TestType type)
    {
        var enabled = await ResolveEnabledTestTypesAsync();
        if (enabled == null || enabled.Contains(type))
        {
            return;
        }

        throw new ArgumentException($"Test type {type} is disabled on this server (EnabledTestTypes)");
    }

    /// <summary>
    /// Parses the EnabledTestTypes setting into the enabled set; null means every type
    /// (a missing setting or an empty array). Throws ArgumentException when the setting
    /// holds something other than a JSON array of type names.
    /// </summary>
    private async Task<HashSet<TestType>?> ResolveEnabledTestTypesAsync()
    {
        var raw = await _settingsService.GetAsync<string>("EnabledTestTypes");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        List<TestType>? enabled;
        try
        {
            enabled = JsonSerializer.Deserialize<List<TestType>>(raw, TestTypeListOptions);
        }
        catch (JsonException)
        {
            throw new ArgumentException("EnabledTestTypes must be a JSON array of test type names, e.g. [\"Ping\",\"Http\",\"Dns\"]");
        }

        return enabled is { Count: > 0 } ? enabled.ToHashSet() : null;
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
    /// Loads a test with its targeting and header rows, for units that resolve targets
    /// or copy headers onto jobs.
    /// </summary>
    private static async Task<Test?> LoadTestAsync(ObiconDbContext db, Guid id)
    {
        return await db.Tests
            .Include(t => t.NodeTargets)
            .Include(t => t.PoolTargets)
            .Include(t => t.Headers)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// Creates the test's jobs with the given context, so callers that already hold a
    /// context for their own read-modify-write unit reuse it instead of opening another.
    /// </summary>
    private async Task EnqueueJobsForTestAsync(ObiconDbContext db, Test test)
    {
        // One job per targeted node and IP family: direct node IDs plus all pool
        // members, deduplicated; Both schedules one IPv4 and one IPv6 job per node
        foreach (var nodeId in await ResolveTargetNodesAsync(db, test))
        {
            foreach (var ipVersion in FamiliesFor(test.IpVersion))
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
                    IpVersion = ipVersion,
                    ExpectedBodyPattern = test.ExpectedBodyPattern,
                    Headers = test.Headers.Select(h => new TestJobHeader { Name = h.Name, Value = h.Value }).ToList(),
                    ProxyUrl = test.ProxyUrl,
                    CacheBust = test.CacheBust,
                    TracerouteMaxHops = test.TracerouteMaxHops,
                    TracerouteQueriesPerHop = test.TracerouteQueriesPerHop,
                    TracerouteQueryTimeoutMs = test.TracerouteQueryTimeoutMs,
                    TracerouteResolveHostnames = test.TracerouteResolveHostnames,
                    PingCount = test.PingCount,
                    PingTimeoutMs = test.PingTimeoutMs,
                    PingIntervalMs = test.PingIntervalMs,
                    HttpMethod = test.HttpMethod,
                    FollowRedirects = test.FollowRedirects,
                    DnsNameserver = test.DnsNameserver,
                    DnsQueryType = test.DnsQueryType
                });
            }
        }
    }

    /// <summary>
    /// The IP families a test runs against: Both becomes one IPv4 and one IPv6
    /// execution; any other value runs as itself. Default: single value.
    /// </summary>
    private static IEnumerable<IpVersion> FamiliesFor(IpVersion ipVersion)
    {
        return ipVersion == IpVersion.Both ? [IpVersion.Ipv4, IpVersion.Ipv6] : [ipVersion];
    }

    /// <summary>
    /// Runs a test once on each selected connected node without creating a test first.
    /// Explicit node IDs run directly; pools contribute their top 3 connected members,
    /// least busy first. Validation errors throw ArgumentException for a 400 response.
    /// </summary>
    public async Task<List<Models.TestJob>> RunOnceAsync(Models.Requests.RunTestOnceRequest request)
    {
        await ValidateTestTypeEnabledAsync(request.Type);
        ValidateHttpExpectations(request.Type, request.ExpectedBodyPattern, request.Headers, request.ProxyUrl);

        if (request.NodeIds.Count == 0 && request.PoolIds.Count == 0)
        {
            throw new ArgumentException("At least one node ID or pool ID is required for a run-once");
        }

        // One unit of work: node and pool existence, selection, and job creation
        // happen together, so the selection sees a consistent database state
        var jobs = await _dbFactory.ExecuteAsync(async db =>
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
                foreach (var memberId in await db.PoolMembers
                             .Where(pm => pm.PoolId == poolId)
                             .Select(pm => pm.NodeId)
                             .ToListAsync())
                {
                    if (!selectedNodeIds.Contains(memberId) && !poolCandidates.Contains(memberId))
                    {
                        poolCandidates.Add(memberId);
                    }
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

            // One job per selected node and IP family; Both schedules one per family
            var created = new List<Models.TestJob>();
            foreach (var nodeId in selectedNodeIds)
            {
                foreach (var ipVersion in FamiliesFor(request.IpVersion))
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
                        IpVersion = ipVersion,
                        ExpectedBodyPattern = request.ExpectedBodyPattern,
                        Headers = (request.Headers ?? new Dictionary<string, string>())
                            .Select(kv => new TestJobHeader { Name = kv.Key, Value = kv.Value })
                            .ToList(),
                        ProxyUrl = request.ProxyUrl,
                        CacheBust = request.CacheBust,
                        TracerouteMaxHops = request.TracerouteMaxHops,
                        TracerouteQueriesPerHop = request.TracerouteQueriesPerHop,
                        TracerouteQueryTimeoutMs = request.TracerouteQueryTimeoutMs,
                        TracerouteResolveHostnames = request.TracerouteResolveHostnames,
                        PingCount = request.PingCount,
                        PingTimeoutMs = request.PingTimeoutMs,
                        PingIntervalMs = request.PingIntervalMs,
                        HttpMethod = request.HttpMethod,
                        FollowRedirects = request.FollowRedirects,
                        DnsNameserver = request.DnsNameserver,
                        DnsQueryType = request.DnsQueryType
                    }));
                }
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
        return await db.Tests
            .Include(t => t.NodeTargets)
            .Include(t => t.PoolTargets)
            .Include(t => t.Headers)
            .Where(t => t.IsActive && t.NodeTargets.Any(n => n.NodeId == nodeId))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
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

    /// <summary>
    /// The nodes a test runs on: its direct targets plus the members of its targeted
    /// pools, deduplicated. Read from the join tables in the given context.
    /// </summary>
    private static async Task<List<Guid>> ResolveTargetNodesAsync(ObiconDbContext db, Test test)
    {
        var nodeIds = new HashSet<Guid>(test.NodeTargets.Select(t => t.NodeId));
        var poolIds = test.PoolTargets.Select(t => t.PoolId).ToList();
        if (poolIds.Count > 0)
        {
            var members = await db.PoolMembers
                .Where(pm => poolIds.Contains(pm.PoolId))
                .Select(pm => pm.NodeId)
                .ToListAsync();
            foreach (var memberId in members)
            {
                nodeIds.Add(memberId);
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
        NodeIds = test.NodeTargets.Select(t => t.NodeId).ToList(),
        PoolIds = test.PoolTargets.Select(t => t.PoolId).ToList(),
        Frequency = test.Frequency,
        IsActive = test.IsActive,
        ExpectedStatusCodes = test.ExpectedStatusCodes,
        CheckCertificateExpiryDays = test.CheckCertificateExpiryDays,
        ExpectedDnsResult = test.ExpectedDnsResult,
        IpVersion = test.IpVersion,
        TimeoutSeconds = test.TimeoutSeconds,
        ExpectedBodyPattern = test.ExpectedBodyPattern,
        Headers = test.Headers.ToDictionary(h => h.Name, h => h.Value),
        ProxyUrl = test.ProxyUrl,
        CacheBust = test.CacheBust,
        TracerouteMaxHops = test.TracerouteMaxHops,
        TracerouteQueriesPerHop = test.TracerouteQueriesPerHop,
        TracerouteQueryTimeoutMs = test.TracerouteQueryTimeoutMs,
        TracerouteResolveHostnames = test.TracerouteResolveHostnames,
        PingCount = test.PingCount,
        PingTimeoutMs = test.PingTimeoutMs,
        PingIntervalMs = test.PingIntervalMs,
        HttpMethod = test.HttpMethod,
        FollowRedirects = test.FollowRedirects,
        DnsNameserver = test.DnsNameserver,
        DnsQueryType = test.DnsQueryType,
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
