using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Query parameters of the test runs listing.
/// </summary>
public class TestRunsQueryParameters
{
    /// <summary>
    /// Page size. Default: 50, range 1 to 500.
    /// </summary>
    public int Limit { get; set; } = 50;

    /// <summary>
    /// Zero-based offset of the first run in the page. Default: 0.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Only runs with this status. Null for every status. Default: null.
    /// </summary>
    public TestJobStatus? Status { get; set; }

    /// <summary>
    /// Only runs executed by this node. Null for every node. Default: null.
    /// </summary>
    public Guid? NodeId { get; set; }

    /// <summary>
    /// Only runs of this test. Null for every test. Default: null.
    /// </summary>
    public Guid? TestId { get; set; }

    /// <summary>
    /// Case-insensitive text matched against target, output, and error message.
    /// Null or empty for no search. Default: null.
    /// </summary>
    public string? Search { get; set; }
}
