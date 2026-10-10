using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models;

/// <summary>
/// The columns a test runs listing can be sorted by.
/// </summary>
public enum TestRunSortBy
{
    /// <summary>
    /// Creation time of the run. The default sort column.
    /// </summary>
    CreatedAt,

    /// <summary>
    /// Execution duration; runs without a duration (queued, no-run) sort first ascending.
    /// </summary>
    DurationMs,

    /// <summary>
    /// Terminal state of the run.
    /// </summary>
    Status
}

/// <summary>
/// A page request for finished and in-flight test runs: page window, sorting, and
/// optional server-side filters. Search matches the target, output, and error message.
/// </summary>
/// <param name="Limit">Page size, 1 to 500.</param>
/// <param name="Offset">Zero-based offset of the first run in the page.</param>
/// <param name="Status">Only runs with this status. Null for every status.</param>
/// <param name="NodeId">Only runs executed by this node. Null for every node.</param>
/// <param name="TestId">Only runs of this test. Null for every test.</param>
/// <param name="Search">Case-insensitive text matched against target, output, and error message. Null for no search.</param>
/// <param name="SortBy">Column the runs are sorted by. Default: CreatedAt.</param>
/// <param name="Descending">True for descending sort order. Default: true (newest first).</param>
public record TestRunQuery(
    int Limit,
    int Offset,
    TestJobStatus? Status,
    Guid? NodeId,
    Guid? TestId,
    string? Search,
    TestRunSortBy SortBy = TestRunSortBy.CreatedAt,
    bool Descending = true);
