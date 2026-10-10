namespace Obicon.Server.Models;

/// <summary>
/// One page of a list query: the items of the requested window and the total
/// number of rows matching the query, so callers can page through.
/// </summary>
/// <typeparam name="T">Item type of the page.</typeparam>
/// <param name="Items">The items of this page, in the query's order.</param>
/// <param name="Total">Total rows matching the query, across all pages.</param>
/// <param name="Limit">Page size that was requested.</param>
/// <param name="Offset">Offset the page starts at.</param>
public record Page<T>(List<T> Items, int Total, int Limit, int Offset);
