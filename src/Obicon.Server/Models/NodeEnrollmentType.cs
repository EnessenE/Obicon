namespace Obicon.Server.Models;

/// <summary>
/// How a node came to exist on the server.
/// </summary>
public enum NodeEnrollmentType
{
    /// <summary>
    /// Created by a user through the API or UI; users manage its name, labels, and pools.
    /// </summary>
    Manual,

    /// <summary>
    /// The node enrolled itself with an enroll token; it manages its own name, labels, and pools.
    /// </summary>
    AutoEnrollment
}
