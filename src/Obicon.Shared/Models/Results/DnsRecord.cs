namespace Obicon.Shared.Models.Results;

/// <summary>
/// One DNS record returned by a nameserver.
/// </summary>
public class DnsRecord
{
    /// <summary>
    /// Record type, e.g. "A", "AAAA", "CNAME", "TXT", "MX", or "CAA". Default: empty string.
    /// </summary>
    public string RecordType { get; set; } = string.Empty;

    /// <summary>
    /// Record value: an address, hostname, text, or "preference host" for MX.
    /// Default: empty string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Time to live of the record in seconds. -1 when the response carried none.
    /// </summary>
    public long TtlSeconds { get; set; } = -1;
}
