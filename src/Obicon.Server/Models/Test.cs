using Obicon.Server.Models.Enums;

namespace Obicon.Server.Models;

public class Test
{
    /// <summary>
    /// Unique identifier for the test. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

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
    /// Indicates if the test is currently active and should be scheduled. Default: true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the test was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last update to the test. Null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
