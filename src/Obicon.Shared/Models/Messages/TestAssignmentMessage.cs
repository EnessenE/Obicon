using Obicon.Shared.Models.Enums;
using System.Text.Json.Serialization;

namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent by the server to assign a single test run to a node.
/// </summary>
public class TestAssignmentMessage
{
    /// <summary>
    /// Unique identifier of the test job. Echoed back in result and status messages.
    /// </summary>
    [JsonPropertyName("JobId")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the test to execute.
    /// </summary>
    [JsonPropertyName("TestId")]
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types.
    /// </summary>
    [JsonPropertyName("TestType")]
    public TestType TestType { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest.
    /// </summary>
    [JsonPropertyName("Target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Maximum execution time for this test in seconds. Default: 60. Capped at 60.
    /// </summary>
    [JsonPropertyName("TimeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 60;
}
