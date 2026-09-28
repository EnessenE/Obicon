using Obicon.Server.Models;
using Obicon.Server.Models.Enums;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public class TestService : ITestService
{
    private readonly List<Test> _tests = new();
    private readonly INodeService _nodeService;

    public TestService(INodeService nodeService)
    {
        _nodeService = nodeService;
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
            NodeIds = request.NodeIds,
            Frequency = request.Frequency,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        _tests.Add(test);

        return new TestResponse
        {
            Id = test.Id,
            Name = test.Name,
            Type = test.Type,
            NodeIds = test.NodeIds,
            Frequency = test.Frequency,
            IsActive = test.IsActive,
            CreatedAt = test.CreatedAt,
            UpdatedAt = test.UpdatedAt
        };
    }

    public Task<IEnumerable<TestResponse>> GetAllTestsAsync()
    {
        var responses = _tests.Select(t => new TestResponse
        {
            Id = t.Id,
            Name = t.Name,
            Type = t.Type,
            NodeIds = t.NodeIds,
            Frequency = t.Frequency,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        });
        return Task.FromResult(responses);
    }

    public Task<TestResponse?> GetTestAsync(Guid id)
    {
        var test = _tests.FirstOrDefault(t => t.Id == id);
        if (test == null)
            return Task.FromResult<TestResponse?>(null);

        return Task.FromResult<TestResponse?>(new TestResponse
        {
            Id = test.Id,
            Name = test.Name,
            Type = test.Type,
            NodeIds = test.NodeIds,
            Frequency = test.Frequency,
            IsActive = test.IsActive,
            CreatedAt = test.CreatedAt,
            UpdatedAt = test.UpdatedAt
        });
    }

    public Task<TestResponse?> UpdateTestAsync(Guid id, TestType type, List<Guid> nodeIds, TestFrequency frequency, bool isActive)
    {
        var test = _tests.FirstOrDefault(t => t.Id == id);
        if (test == null)
            return Task.FromResult<TestResponse?>(null);

        test.Type = type;
        test.NodeIds = nodeIds;
        test.Frequency = frequency;
        test.IsActive = isActive;
        test.UpdatedAt = DateTime.UtcNow;

        return Task.FromResult<TestResponse?>(new TestResponse
        {
            Id = test.Id,
            Name = test.Name,
            Type = test.Type,
            NodeIds = test.NodeIds,
            Frequency = test.Frequency,
            IsActive = test.IsActive,
            CreatedAt = test.CreatedAt,
            UpdatedAt = test.UpdatedAt
        });
    }

    public Task<bool> DeleteTestAsync(Guid id)
    {
        var test = _tests.FirstOrDefault(t => t.Id == id);
        if (test == null)
            return Task.FromResult(false);

        _tests.Remove(test);
        return Task.FromResult(true);
    }

    public Task<bool> TriggerTestRunAsync(Guid testId)
    {
        var test = _tests.FirstOrDefault(t => t.Id == testId && t.IsActive);
        if (test == null)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<IEnumerable<Test>> GetTestsForNodeAsync(Guid nodeId)
    {
        var tests = _tests.Where(t => t.NodeIds.Contains(nodeId) && t.IsActive);
        return Task.FromResult(tests);
    }
}
