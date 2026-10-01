using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface ITestService
{
    Task<TestResponse> CreateTestAsync(CreateTestRequest request);
    Task<IEnumerable<TestResponse>> GetAllTestsAsync();
    Task<TestResponse?> GetTestAsync(Guid id);
    Task<TestResponse?> UpdateTestAsync(Guid id, Models.Requests.UpdateTestRequest request);
    Task<TestResponse?> ToggleTestAsync(Guid id);
    Task<bool> DeleteTestAsync(Guid id);
    Task<bool> TriggerTestRunAsync(Guid testId);
    Task<int> ScheduleDueTestsAsync();
    /// <summary>
    /// Runs a test once on each of the requested connected nodes without creating a test.
    /// Throws ArgumentException for unknown nodes or when none of them are connected.
    /// </summary>
    Task<List<Models.TestJob>> RunOnceAsync(Models.Requests.RunTestOnceRequest request);
    Task<IEnumerable<Test>> GetTestsForNodeAsync(Guid nodeId);
}
