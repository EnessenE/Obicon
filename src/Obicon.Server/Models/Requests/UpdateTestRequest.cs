using System.ComponentModel.DataAnnotations;
using Obicon.Server.Models.Enums;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Requests;

public class UpdateTestRequest
{
    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types.
    /// </summary>
    [EnumDataType(typeof(TestType))]
    public TestType Type { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Required.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// List of node IDs that should execute this test. At least one node is required. Default: empty list.
    /// </summary>
    [MinLength(1)]
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// How often the test should be executed. See <see cref="TestFrequency"/> for available frequencies.
    /// </summary>
    [EnumDataType(typeof(TestFrequency))]
    public TestFrequency Frequency { get; set; }

    /// <summary>
    /// Indicates if the test should be active. Default: false.
    /// </summary>
    public bool IsActive { get; set; }
}
