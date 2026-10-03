namespace Obicon.Node.Services;

/// <summary>
/// Counters describing the test execution state of the node. Running and Pending are gauges; the rest are monotonic.
/// </summary>
public class NodeStatistics
{
    private long _running;
    private long _pending;
    private long _completed;
    private long _failed;
    private long _timedOut;

    /// <summary>
    /// Number of tests currently executing. Default: 0.
    /// </summary>
    public long Running => _running;

    /// <summary>
    /// Number of assigned tests waiting for a free execution slot. Default: 0.
    /// </summary>
    public long Pending => _pending;

    /// <summary>
    /// Number of tests finished successfully. Default: 0.
    /// </summary>
    public long Completed => _completed;

    /// <summary>
    /// Number of tests that finished unsuccessfully. Default: 0.
    /// </summary>
    public long Failed => _failed;

    /// <summary>
    /// Number of tests that exceeded their timeout. Default: 0.
    /// </summary>
    public long TimedOut => _timedOut;

    /// <summary>
    /// Registers that a test was accepted and is waiting for a free slot.
    /// </summary>
    public void IncrementPending() => Interlocked.Increment(ref _pending);

    /// <summary>
    /// Registers that a waiting test left the pending queue, e.g. because it started or was rejected.
    /// </summary>
    public void DecrementPending() => Interlocked.Decrement(ref _pending);

    /// <summary>
    /// Registers that a test started executing.
    /// </summary>
    public void IncrementRunning() => Interlocked.Increment(ref _running);

    /// <summary>
    /// Registers that a running test finished, whatever the outcome.
    /// </summary>
    public void DecrementRunning() => Interlocked.Decrement(ref _running);

    /// <summary>
    /// Registers that a test finished successfully.
    /// </summary>
    public void IncrementCompleted() => Interlocked.Increment(ref _completed);

    /// <summary>
    /// Registers that a test finished unsuccessfully.
    /// </summary>
    public void IncrementFailed() => Interlocked.Increment(ref _failed);

    /// <summary>
    /// Registers that a test exceeded its timeout.
    /// </summary>
    public void IncrementTimedOut() => Interlocked.Increment(ref _timedOut);
}
