namespace Obicon.Server.Models;

/// <summary>
/// One DNS record returned by a DNS run.
/// </summary>
public class TestJobDnsRecord
{
    /// <summary>
    /// Unique identifier of the record row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the job the record belongs to. Cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Position of the record within the run, 0-based; orders the records.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// The record type, e.g. "A" or "CNAME". Default: empty string.
    /// </summary>
    public string RecordType { get; set; } = string.Empty;

    /// <summary>
    /// The record value. Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The record's time to live, in seconds. -1 when unknown. Default: -1.
    /// </summary>
    public long TtlSeconds { get; set; } = -1;
}
