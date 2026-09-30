namespace Obicon.Server.Models.Responses;

/// <summary>
/// A plain message response, e.g. for actions without a result body or error details.
/// </summary>
public class MessageResponse
{
    /// <summary>
    /// Human-readable message. Default: empty string.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
