using System.Text.Json;

namespace Obicon.Node.Services;

/// <summary>
/// Persists the node's enrolled identity (node ID + auth token) in node-identity.json
/// next to the working directory, so restarts do not re-enroll.
/// </summary>
public class NodeIdentityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    /// <summary>
    /// Node ID assigned by the server. Null when the node has not enrolled yet.
    /// </summary>
    public string? NodeId { get; private set; }

    /// <summary>
    /// Auth token received at enrollment. Null when the node has not enrolled yet.
    /// </summary>
    public string? AuthToken { get; private set; }

    /// <summary>
    /// Server version seen on the last connection, e.g. "0.2.0". Null when never connected.
    /// Used to log a notice when the server is upgraded or downgraded.
    /// </summary>
    public string? LastServerVersion { get; private set; }

    public NodeIdentityStore(string path = "node-identity.json")
    {
        _path = path;
        Load();
    }

    /// <summary>
    /// Clears the stored identity, e.g. after the server rejected the token.
    /// </summary>
    public void Reset()
    {
        NodeId = null;
        AuthToken = null;
        try
        {
            File.Delete(_path);
        }
        catch (Exception)
        {
            // best effort
        }
    }

    /// <summary>
    /// Saves the identity to disk.
    /// </summary>
    public void Save(string nodeId, string authToken)
    {
        NodeId = nodeId;
        AuthToken = authToken;
        File.WriteAllText(_path, JsonSerializer.Serialize(new
        {
            NodeId = nodeId,
            AuthToken = authToken,
            LastServerVersion = LastServerVersion
        }, JsonOptions));
    }

    /// <summary>
    /// Remembers the server version seen on the last connection so a change can be logged.
    /// </summary>
    public void SaveServerVersion(string serverVersion)
    {
        LastServerVersion = serverVersion;

        // The identity file may not exist on manually registered nodes; only rewrite it when it does
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            var json = JsonSerializer.Serialize(new
            {
                NodeId = document.RootElement.TryGetProperty(nameof(NodeId), out var id) ? id.GetString() : null,
                AuthToken = document.RootElement.TryGetProperty(nameof(AuthToken), out var token) ? token.GetString() : null,
                LastServerVersion = serverVersion
            }, JsonOptions);
            File.WriteAllText(_path, json);
        }
        catch (Exception)
        {
            // best effort: the version notice is not worth failing the connection over
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            NodeId = document.RootElement.TryGetProperty(nameof(NodeId), out var id) ? id.GetString() : null;
            AuthToken = document.RootElement.TryGetProperty(nameof(AuthToken), out var token) ? token.GetString() : null;
            LastServerVersion = document.RootElement.TryGetProperty(nameof(LastServerVersion), out var version) ? version.GetString() : null;
        }
        catch (Exception)
        {
            // Corrupt or unreadable identity file: fall back to enrollment
            NodeId = null;
            AuthToken = null;
        }
    }
}
