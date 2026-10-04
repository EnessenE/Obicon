namespace Obicon.Server.Models.Responses;

/// <summary>
/// One page of a list, as exposed by the API: the items of the requested window
/// and the total number of rows matching the query across all pages.
/// </summary>
/// <typeparam name="T">Item type of the page.</typeparam>
public class PageResponse<T>
{
    /// <summary>
    /// The items of this page, in the query's order.
    /// </summary>
    public List<T> Items { get; set; } = [];

    /// <summary>
    /// Total rows matching the query, across all pages.
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
