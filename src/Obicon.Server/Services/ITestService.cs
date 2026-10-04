using Obicon.Server.Models;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface ITestService
{
    /// <summary>
    /// Every test type with its current enabled state, resolved from the
    /// EnabledTestTypes setting. Throws ArgumentException when the setting holds
    /// something other than a JSON array of type names.
    /// </summary>
    Task<List<Models.Responses.TestTypeInfo>> GetTestTypesAsync();

    Task<TestResponse> CreateTestAsync(TestRequest request);
    Task<Page<TestResponse>> GetTestsAsync(PageParameters page);
    /// <summary>
    /// Number of tests, optionally only the active ones, for the stats endpoint;
    /// avoids loading a page.
    /// </summary>
    Task<int> GetTestCountAsync(bool? isActive = null);
    Task<TestResponse?> GetTestAsync(Guid id);
    Task<TestResponse?> UpdateTestAsync(Guid id, Models.Requests.TestRequest request);
    Task<TestResponse?> SetTestActiveAsync(Guid id, bool isActive);
    Task<bool> DeleteTestAsync(Guid id);
    Task<List<Models.TestJob>> RunTestAsync(Guid testId);
    Task<int> ScheduleDueTestsAsync();
    /// <summary>
    /// Runs a test once on each of the requested connected nodes without creating a test.
    /// Throws ArgumentException for unknown nodes or when none of them are connected.
    /// </summary>
    Task<List<Models.TestJob>> RunOnceAsync(Models.Requests.RunTestOnceRequest request);
    Task<List<TestResponse>> GetTestsForNodeAsync(Guid nodeId);
}
