namespace Obicon.Server.Models;

/// <summary>
/// A custom HTTP header attached to a test, sent with its requests. One row per
/// header; rows cascade on test deletion. Default: empty table.
/// </summary>
public class TestHeader
{
    /// <summary>
    /// ID of the test the header belongs to. Part of the primary key, cascades on test deletion.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// The header name, e.g. "Authorization". Part of the primary key. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The header value. Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
