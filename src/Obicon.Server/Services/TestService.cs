using Microsoft.EntityFrameworkCore;
using Obicon.Server.Data;
using Obicon.Server.WebSockets;
using Obicon.Server.Models;
using Obicon.Server.Models.Enums;
using Obicon.Shared.Models.Enums;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class TestService : ITestService
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly INodeService _nodeService;
    private readonly ITestQueueService _queueService;
    private readonly NodeConnectionManager _connectionManager;

    public TestService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        INodeService nodeService,
        ITestQueueService queueService,
        NodeConnectionManager connectionManager)
    {
        _dbFactory = dbFactory;
        _nodeService = nodeService;
        _queueService = queueService;
        _connectionManager = connectionManager;
    }

    public async Task<TestResponse> CreateTestAsync(CreateTestRequest request)
    {
        foreach (var nodeId in request.NodeIds)
        {
            var node = await _nodeService.GetNodeAsync(nodeId);
            if (node == null)
            {
                throw new ArgumentException("Invalid node ID: " + nodeId);
            }
        }

        var test = new Test
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Target = request.Target,
            NodeIds = request.NodeIds,
            Frequency = request.Frequency,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Tests.Add(test);
        await db.SaveChangesAsync();

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

    public async Task<TestResponse?> UpdateTestAsync(Guid id, TestType type, string target, List<Guid> nodeIds, TestFrequency frequency, bool isActive)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var test = await db.Tests.FindAsync(id);
        if (test == null)
        {
            return null;
        }

        test.Type = type;
        test.Target = target;
        test.NodeIds = nodeIds;
        test.Frequency = frequency;
        test.IsActive = isActive;
        test.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

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

        // Enqueue one job per assigned node; dispatch to nodes happens in the queue processor
        foreach (var nodeId in test.NodeIds)
        {
            await _queueService.EnqueueTestAsync(testId, nodeId, test.Type, test.Target);
        }

        return true;
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

        return await _queueService.EnqueueTestAsync(
            Guid.Empty, request.NodeId, request.Type, request.Target, request.TimeoutSeconds);
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

    private static TestResponse ToResponse(Test test) => new()
    {
        Id = test.Id,
        Name = test.Name,
        Type = test.Type,
        Target = test.Target,
        NodeIds = test.NodeIds,
        Frequency = test.Frequency,
        IsActive = test.IsActive,
        CreatedAt = test.CreatedAt,
        UpdatedAt = test.UpdatedAt
    };
}
