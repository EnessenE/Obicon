namespace Obicon.Shared.Models.Messages;

/// <summary>
/// One log entry shipped from a node to the server. The server only accepts these
/// while the NodeLogShippingEnabled setting is on, and only writes them to its own
/// console while ShipNodeLogsToConsole is on.
/// </summary>
public class NodeLogMessage
{
    /// <summary>
    /// Unique identifier of the node that produced the entry. Default: empty string.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the node at the time of logging. Default: empty string.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the node software, e.g. "0.2.0". Default: empty string.
    /// </summary>
    public string NodeVersion { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the log entry.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Severity of the entry: "Debug", "Information", "Warning", or "Error". Default: empty string.
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// The log message with its template placeholders filled in. Default: empty string.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Exception details when the entry was caused by an exception. Null otherwise.
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Structured properties attached to the entry: source context, scope properties
    /// (e.g. JobId, TestId), and any named values of the log call. Null when none.
    /// </summary>
    public Dictionary<string, string>? Properties { get; set; }
}
