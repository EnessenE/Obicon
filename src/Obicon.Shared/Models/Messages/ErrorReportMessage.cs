namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Message sent by node to report errors during execution.
/// </summary>
public class ErrorReportMessage
{
    /// <summary>
    /// ID of the node that encountered the error.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Type/category of the error.
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the error.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Stack trace of the error. Default: null.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
