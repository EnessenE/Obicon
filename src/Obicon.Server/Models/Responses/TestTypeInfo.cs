using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Responses;

/// <summary>
/// One test type and whether the server currently offers it.
/// </summary>
public class TestTypeInfo
{
    /// <summary>
    /// The test type's enum value, e.g. 0 for Ping.
    /// </summary>
    public TestType Type { get; set; }

    /// <summary>
    /// The test type's name, e.g. "Ping". Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the type can be used on this server. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
