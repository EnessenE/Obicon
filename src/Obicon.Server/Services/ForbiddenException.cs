namespace Obicon.Server.Services;

/// <summary>
/// Thrown when an action is understood but explicitly not allowed for the caller,
/// regardless of authentication; the API maps it to 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    /// <summary>
    /// Creates the exception with the message surfaced to the client.
    /// </summary>
    public ForbiddenException(string message) : base(message)
    {
    }
}
