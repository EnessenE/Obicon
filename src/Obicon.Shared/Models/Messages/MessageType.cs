namespace Obicon.Shared.Models.Messages;

/// <summary>
/// Types of messages exchanged between server and nodes via WebSocket.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Sent by node when it first connects to register itself.
    /// </summary>
    NodeRegistration,

    /// <summary>
    /// Sent periodically by node to indicate it's alive. Default: every 1 second.
    /// </summary>
    NodeHeartbeat,

    /// <summary>
    /// Sent by server to assign a test to a node.
    /// </summary>
    TestAssignment,

    /// <summary>
    /// Sent by node to report test execution results.
    /// </summary>
    TestResult,

    /// <summary>
    /// Sent by node to update test execution status.
    /// </summary>
    TestStatusUpdate,

    /// <summary>
    /// Sent by node to report errors.
    /// </summary>
    ErrorReport
}
