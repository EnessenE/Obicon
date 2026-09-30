using Obicon.Server.Services;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Unit tests for the run-once pool selection: only connected nodes are eligible,
/// the least busy ones come first, and the selection is capped.
/// </summary>
public class RunOnceSelectionTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public void SelectTopNodes_ReturnsOnlyConnectedCandidates()
    {
        var disconnected = NewId();
        var connected = NewId();

        var selected = TestService.SelectTopNodes(
            new[] { disconnected, connected },
            new HashSet<Guid> { connected },
            new Dictionary<Guid, int>(),
            TestService.PoolSelectionLimit);

        Assert.Equal([connected], selected);
    }

    [Fact]
    public void SelectTopNodes_PrefersTheLeastBusy()
    {
        var busy = NewId();
        var idle = NewId();
        var mid = NewId();

        var selected = TestService.SelectTopNodes(
            new[] { busy, mid, idle },
            new HashSet<Guid> { busy, mid, idle },
            new Dictionary<Guid, int>
            {
                [busy] = 7,
                [mid] = 3,
                [idle] = 0
            },
            TestService.PoolSelectionLimit);

        Assert.Equal([idle, mid, busy], selected);
    }

    [Fact]
    public void SelectTopNodes_IsCappedAtTheLimit()
    {
        var nodes = Enumerable.Range(0, 10).Select(_ => NewId()).ToList();

        var selected = TestService.SelectTopNodes(
            nodes,
            nodes.ToHashSet(),
            new Dictionary<Guid, int>(),
            TestService.PoolSelectionLimit);

        Assert.Equal(TestService.PoolSelectionLimit, selected.Count);
    }

    [Fact]
    public void SelectTopNodes_TreatsUnknownNodesAsIdle()
    {
        // Nodes with no active jobs dictionary entry count as zero busy, keeping input order
        var first = NewId();
        var second = NewId();

        var selected = TestService.SelectTopNodes(
            new[] { first, second },
            new HashSet<Guid> { first, second },
            new Dictionary<Guid, int> { [second] = 2 },
            TestService.PoolSelectionLimit);

        Assert.Equal([first, second], selected);
    }

    [Fact]
    public void SelectTopNodes_EmptyCandidates_SelectNothing()
    {
        var selected = TestService.SelectTopNodes(
            Array.Empty<Guid>(),
            new HashSet<Guid> { NewId() },
            new Dictionary<Guid, int>(),
            TestService.PoolSelectionLimit);

        Assert.Empty(selected);
    }
}
