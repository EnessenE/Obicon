
namespace Obicon.Server.Models;

/// <summary>
/// One page of test runs: the loaded jobs of the requested window and the total
/// number of runs matching the filters, so callers can page through.
/// </summary>
/// <param name="Items">The jobs of this page, newest first.</param>
/// <param name="Total">Total runs matching the filters, across all pages.</param>
/// <param name="Limit">Page size that was requested.</param>
/// <param name="Offset">Offset the page starts at.</param>
public record TestRunPage(List<TestJob> Items, int Total, int Limit, int Offset);
