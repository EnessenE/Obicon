namespace Obicon.Server.Models;

/// <summary>
/// A node directly targeted by a test (as opposed to via a pool). One row per
/// targeted node; membership cascades on test or node deletion. Default: empty table.
/// </summary>
public class TestTargetNode
{
    /// <summary>
    /// ID of the test. Part of the primary key, cascades on test deletion.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// ID of the targeted node. Part of the primary key, cascades on node deletion.
    /// </summary>
    public Guid NodeId { get; set; }
}

/// <summary>
/// A pool targeted by a test: the test runs on the pool's members. One row per
/// targeted pool; membership cascades on test or pool deletion. Default: empty table.
/// </summary>
public class TestTargetPool
{
    /// <summary>
    /// ID of the test. Part of the primary key, cascades on test deletion.
    /// </summary>
    public Guid TestId { get; set; }

    /// <summary>
    /// ID of the targeted pool. Part of the primary key, cascades on pool deletion.
    /// </summary>
    public Guid PoolId { get; set; }
}

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

/// <summary>
/// A custom HTTP header copied onto a job from its test at enqueue time, so the
/// dispatched assignment is immune to later test edits. Rows cascade on job deletion.
/// Default: empty table.
/// </summary>
public class TestJobHeader
{
    /// <summary>
    /// ID of the job the header belongs to. Part of the primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The header name. Part of the primary key. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The header value. Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
