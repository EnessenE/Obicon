using System.ComponentModel.DataAnnotations;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Pagination parameters shared by every list endpoint: a page of at most
/// <see cref="Limit"/> items starting at <see cref="Offset"/>.
/// </summary>
public class PageParameters
{
    /// <summary>
    /// Largest page any list endpoint serves, so one request can never load an unbounded window.
    /// </summary>
    public const int MaxLimit = 500;

    /// <summary>
    /// Page size. Default: 100, range 1 to 500.
    /// </summary>
    [Range(1, MaxLimit)]
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Zero-based offset of the first item in the page. Default: 0.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Offset { get; set; }
}
