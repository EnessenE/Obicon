
namespace Obicon.Server.Models.Responses;

/// <summary>
/// One page of test runs, as exposed by the API.
/// </summary>
public class TestRunsPageResponse
{
    /// <summary>
    /// The runs of this page, newest first.
    /// </summary>
    public List<TestJobResponse> Items { get; set; } = [];

    /// <summary>
    /// Total runs matching the filters, across all pages.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Page size that was requested.
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// Offset the page starts at.
    /// </summary>
    public int Offset { get; set; }
}
