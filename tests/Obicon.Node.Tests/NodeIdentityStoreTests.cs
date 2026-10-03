using Obicon.Node.Configuration;
using Obicon.Node.Services;
using Xunit;

namespace Obicon.Node.Tests;

/// <summary>
/// Unit tests for the node identity file: persistence across restarts, server version
/// tracking, and recovery from a corrupt file.
/// </summary>
public class NodeIdentityStoreTests : IDisposable
{
    private readonly string _path;

    public NodeIdentityStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"node-identity-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }

    [Fact]
    public void Save_ThenLoad_RestoresIdentity()
    {
        var store = new NodeIdentityStore(_path);
        store.Save("6f2d7f6e-0000-0000-0000-000000000001", "auth-token");

        var reloaded = new NodeIdentityStore(_path);
        Assert.Equal("6f2d7f6e-0000-0000-0000-000000000001", reloaded.NodeId);
        Assert.Equal("auth-token", reloaded.AuthToken);
    }

    [Fact]
    public void Reset_ClearsIdentity_AndDeletesFile()
    {
        var store = new NodeIdentityStore(_path);
        store.Save("6f2d7f6e-0000-0000-0000-000000000002", "auth-token");
        Assert.True(File.Exists(_path));

        store.Reset();

        Assert.Null(store.NodeId);
        Assert.Null(store.AuthToken);
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void SaveServerVersion_UpdatesInMemory_AndPersistsWhenFileExists()
    {
        var store = new NodeIdentityStore(_path);
        store.Save("6f2d7f6e-0000-0000-0000-000000000003", "auth-token");

        store.SaveServerVersion("0.2.0");

        Assert.Equal("0.2.0", store.LastServerVersion);

        // A restart keeps the version notice working
        var reloaded = new NodeIdentityStore(_path);
        Assert.Equal("0.2.0", reloaded.LastServerVersion);
        // And the identity is intact
        Assert.Equal("6f2d7f6e-0000-0000-0000-000000000003", reloaded.NodeId);
    }

    [Fact]
    public void SaveServerVersion_DoesNotCreateFile_WhenIdentityIsMissing()
    {
        var store = new NodeIdentityStore(_path);

        store.SaveServerVersion("0.2.0");

        Assert.Equal("0.2.0", store.LastServerVersion);
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToNoIdentity()
    {
        File.WriteAllText(_path, "not json at all {{{");

        var store = new NodeIdentityStore(_path);

        Assert.Null(store.NodeId);
        Assert.Null(store.AuthToken);
        Assert.Null(store.LastServerVersion);
    }

    [Fact]
    public void NodeInfo_ReportsTheBuildVersion()
    {
        // The version comes from the csproj and must parse as a real version
        Assert.NotEmpty(NodeInfo.Version);
        Assert.NotNull(System.Version.Parse(NodeInfo.Version));
    }
}
