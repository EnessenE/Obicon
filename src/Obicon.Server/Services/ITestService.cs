using Obicon.Server.Models;
using Obicon.Server.Models.Enums;
using Obicon.Server.Models.Requests;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface ITestService
{
    Task<TestResponse> CreateTestAsync(CreateTestRequest request);
    Task<IEnumerable<TestResponse>> GetAllTestsAsync();
    Task<TestResponse?> GetTestAsync(Guid id);
    Task<TestResponse?> UpdateTestAsync(Guid id, TestType type, List<Guid> nodeIds, TestFrequency frequency, bool isActive);
    Task<bool> DeleteTestAsync(Guid id);
    Task<bool> TriggerTestRunAsync(Guid testId);
    Task<IEnumerable<Test>> GetTestsForNodeAsync(Guid nodeId);
}
