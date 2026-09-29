using System.ComponentModel.DataAnnotations;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Runs a single test immediately on one node without creating a test first.
/// </summary>
public class RunTestOnceRequest
{
    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types. Required.
    /// </summary>
    [EnumDataType(typeof(TestType))]
    public TestType Type { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Required.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// ID of the node to execute the test on. Required.
    /// </summary>
    [Required]
    public Guid NodeId { get; set; }

    /// <summary>
    /// Maximum execution time for this run in seconds, between 1 and 60. Default: 60.
    /// </summary>
    [Range(1, 60)]
    public int? TimeoutSeconds { get; set; }
}
