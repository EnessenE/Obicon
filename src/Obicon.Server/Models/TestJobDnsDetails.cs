namespace Obicon.Server.Models;

/// <summary>
/// DNS section of a run's details: one row per finished DNS job.
/// </summary>
public class TestJobDnsDetails
{
    /// <summary>
    /// ID of the job the section belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The queried host. Default: empty string.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// The nameservers that were queried. Default: empty array.
    /// </summary>
    public List<string> NameserversQueried { get; set; } = [];

    /// <summary>
    /// The nameserver that answered. Default: null.
    /// </summary>
    public string? AnsweringNameserver { get; set; }

    /// <summary>
    /// Round trip of the answering nameserver, in milliseconds. Default: null.
    /// </summary>
    public double? NameserverRttMs { get; set; }

    /// <summary>
    /// The address values after the IP version filter, as a native text array. Default: empty array.
    /// </summary>
    public List<string> Resolved { get; set; } = [];

    /// <summary>
    /// How the query was performed: "nameserver", "os-resolver", or "literal". Default: empty string.
    /// </summary>
    public string Via { get; set; } = string.Empty;

    /// <summary>
    /// The answering nameserver's DNS status, e.g. "NOERROR". Default: null.
    /// </summary>
    public string? ResponseStatus { get; set; }

    /// <summary>
    /// The queried record type, e.g. "A". Default: empty string.
    /// </summary>
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// The expected address when one was configured. Default: null.
    /// </summary>
    public string? ExpectedAddress { get; set; }

    /// <summary>
    /// Whether the result matched the expectation. Null when none was configured. Default: null.
    /// </summary>
    public bool? ExpectedMatched { get; set; }

    /// <summary>
    /// Error that failed the run. Null on success. Default: null.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// One row per returned record, ordered by the Ordinal column. Default: empty list.
    /// </summary>
    public List<TestJobDnsRecord> Records { get; set; } = [];
}
