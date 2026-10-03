using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Obicon.Server.Data;

/// <summary>
/// Serializes SQLite writes: mutating work is enqueued as units and executed one by
/// one by a single background consumer, because SQLite handles exactly one writer.
/// Reads are not affected: they go directly to the database, as often as needed.
/// Each queued unit runs with its own DbContext, so a unit must perform its whole
/// read-modify-write itself; entities must not cross the queue boundary.
/// </summary>
public partial class SqliteWriteQueue : IDisposable
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly ILogger<SqliteWriteQueue> _logger;
    private readonly BlockingCollection<WriteWorkItem> _queue = new(new ConcurrentQueue<WriteWorkItem>());
    private readonly Task _drainTask;

    public SqliteWriteQueue(IDbContextFactory<ObiconDbContext> dbFactory, ILogger<SqliteWriteQueue> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        // Actively drain the queue from a single dedicated consumer
        _drainTask = Task.Run(DrainAsync);
    }

    /// <summary>
    /// Queues a write that produces no result. Awaits until the single consumer has
    /// run it; failures propagate to the caller.
    /// </summary>
    public Task EnqueueAsync(Func<ObiconDbContext, Task> write)
    {
        return EnqueueAsync<object?>(async db =>
        {
            await write(db);
            return null;
        });
    }

    /// <summary>
    /// Queues a write and returns its result. Awaits until the single consumer has run
    /// it; failures propagate to the caller.
    /// </summary>
    public Task<T> EnqueueAsync<T>(Func<ObiconDbContext, Task<T>> write)
    {
        var item = new WriteWorkItem
        {
            Work = async db => (object?)await write(db),
            Completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        _queue.Add(item);
        return CastResult<T>(item);
    }

    private static async Task<T> CastResult<T>(WriteWorkItem item)
    {
        await item.Completion.Task;
        return (T)item.Completion.Task.Result!;
    }

    private async Task DrainAsync()
    {
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var result = await item.Work(db);
                item.Completion.TrySetResult(result);
            }
            catch (Exception ex)
            {
                LogWriteFailed(ex);
                item.Completion.TrySetException(ex);
            }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // Stop accepting writes and give the consumer time to finish what is queued
        _queue.CompleteAdding();
        try
        {
            _drainTask.Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            LogDrainFailed(ex);
        }
        _queue.Dispose();
    }

    private sealed class WriteWorkItem
    {
        /// <summary>
        /// The whole read-modify-write unit; runs on the single consumer with its own DbContext.
        /// </summary>
        public Func<ObiconDbContext, Task<object?>> Work = null!;

        /// <summary>
        /// Completed by the consumer when the write is done or failed.
        /// </summary>
        public TaskCompletionSource<object?> Completion = null!;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Queued SQLite write failed")]
    private partial void LogWriteFailed(System.Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SQLite write queue did not drain cleanly within the shutdown window")]
    private partial void LogDrainFailed(System.Exception exception);
}
