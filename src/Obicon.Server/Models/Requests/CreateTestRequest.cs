using Obicon.Server.Models.Enums;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Requests;

public class CreateTestRequest
{
    /// <summary>
    /// Human-readable name of the test. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types.
    /// </summary>
    public TestType Type { get; set; }

    /// <summary>
    /// List of node IDs that should execute this test. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// How often the test should be executed. See <see cref="TestFrequency"/> for available frequencies.
    /// </summary>
    public TestFrequency Frequency { get; set; }

    /// <summary>
    /// Indicates if the test should be active immediately. Default: true.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
