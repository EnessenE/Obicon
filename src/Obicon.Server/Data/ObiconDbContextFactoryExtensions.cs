using Microsoft.EntityFrameworkCore;

namespace Obicon.Server.Data;

/// <summary>
/// Runs a unit of database work against its own short-lived context. Units keep the
/// read-modify-write shape they had under the old write queue, but PostgreSQL handles
/// concurrent writers, so they execute directly instead of waiting for a single consumer.
/// </summary>
public static class ObiconDbContextFactoryExtensions
{
    /// <summary>
    /// Runs a unit of work and returns its result. The unit gets its own DbContext and
    /// must perform its whole read-modify-write itself; entities must not cross the boundary.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(this IDbContextFactory<ObiconDbContext> factory, Func<ObiconDbContext, Task<T>> work)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await work(db);
    }

    /// <summary>
    /// Runs a unit of work that produces no result. The unit gets its own DbContext and
    /// must perform its whole read-modify-write itself; entities must not cross the boundary.
    /// </summary>
    public static async Task ExecuteAsync(this IDbContextFactory<ObiconDbContext> factory, Func<ObiconDbContext, Task> work)
    {
        await using var db = await factory.CreateDbContextAsync();
        await work(db);
    }
}
