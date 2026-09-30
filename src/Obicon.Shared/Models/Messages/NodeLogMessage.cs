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
    /// UTC timestamp of the log entry.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Severity of the entry: "Debug", "Information", "Warning", or "Error". Default: empty string.
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// The log message itself. Default: empty string.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Exception details when the entry was caused by an exception. Null otherwise.
    /// </summary>
    public string? Exception { get; set; }
}
