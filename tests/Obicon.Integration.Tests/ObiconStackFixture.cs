using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace Obicon.Integration.Tests;

/// <summary>
/// Boots the full Obicon stack in Docker via Testcontainers: the server image built
/// from the repo's Dockerfile, and a node image that connects to it over a real
/// WebSocket after enrolling with a token. Tests share one stack through the
/// <see cref="ObiconStackCollectionDefinition"/>.
/// </summary>
public sealed class ObiconStackFixture : IAsyncLifetime
{
    public const string AuthHeader = "integration-test-key";
    private const int ServerPort = 5000;

    private readonly string _runId = Guid.NewGuid().ToString("N");
    private INetwork _network = null!;
    private IContainer _server = null!;
    private IContainer _node = null!;

    /// <summary>
    /// API client with the auth header preconfigured against the server's mapped port.
    /// </summary>
    public HttpClient Api { get; private set; } = null!;

    /// <summary>
    /// The server's public (host) port the API is reachable on. Default: 0.
    /// </summary>
    public int ServerHostPort { get; private set; }

    /// <summary>
    /// ID of the node that enrolled itself over the WebSocket. Default: empty.
    /// </summary>
    public Guid NodeId { get; private set; }

    /// <summary>
    /// The server container's docker network name, resolvable by the node through the
    /// embedded DNS. Default: empty string.
    /// </summary>
    public string ServerContainerName { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            await InitializeStackAsync();
        }
        catch (Exception exception)
        {
            // Boot failures reach every test as an exception; the container logs
            // name the cause, so they travel with the failure message
            var logs = await CollectLogsAsync();
            throw new InvalidOperationException(
                $"The Obicon stack failed to start: {exception.Message}{Environment.NewLine}{logs}", exception);
        }
    }

    private async Task InitializeStackAsync()
    {
        var repoRoot = FindRepositoryRoot();

        _network = new NetworkBuilder()
            .WithName($"obicon-it-{_runId}")
            .Build();

        // Images build from the repo's Dockerfiles, from the repository root context
        var serverImageName = $"obicon-it-server:{_runId}";
        var nodeImageName = $"obicon-it-node:{_runId}";
        await BuildImageAsync(repoRoot, "src/Obicon.Server/Dockerfile", serverImageName);
        await BuildImageAsync(repoRoot, "src/Obicon.Node/Dockerfile", nodeImageName);

        // The server container joins the network under its name, so the node can
        // reach it as a docker DNS name; the API is reachable from the host on a
        // random mapped port
        ServerContainerName = $"obicon-it-server-{_runId}";
        _server = new ContainerBuilder(serverImageName)
            .WithName(ServerContainerName)
            .WithNetwork(_network)
            .WithEnvironment("ServerSettings__AuthHeader", AuthHeader)
            .WithEnvironment("ServerSettings__NodeAutoEnrollmentEnabled", "true")
            .WithPortBinding(ServerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ServerPort))
            .Build();
        await _server.StartAsync();

        ServerHostPort = _server.GetMappedPublicPort(ServerPort);
        Api = new HttpClient { BaseAddress = new Uri($"http://localhost:{ServerHostPort}") };
        Api.DefaultRequestHeaders.Add("Authorization", AuthHeader);

        await WaitForServerHealthAsync();

        // One enroll token; the node registers itself with it on first contact
        var tokenResponse = await Api.PostAsJsonAsync("/v1/enroll-tokens", new { Name = "integration" });
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var enrollToken = token.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The enroll token response did not contain a token");

        _node = new ContainerBuilder(nodeImageName)
            .WithName($"obicon-it-node-{_runId}")
            .WithNetwork(_network)
            .WithEnvironment("Node__NodeName", "integration-node")
            .WithEnvironment("Node__ServerUrl", $"ws://{ServerContainerName}:5000/ws/nodes")
            .WithEnvironment("Node__EnrollToken", enrollToken)
            .WithEnvironment("Node__RequireTls", "false")
            .Build();
        await _node.StartAsync();

        await WaitForConnectedNodeAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        Api?.Dispose();
        if (_node != null)
        {
            await _node.DisposeAsync();
        }

        if (_server != null)
        {
            await _server.DisposeAsync();
        }

        if (_network != null)
        {
            await _network.DeleteAsync();
        }
    }

    /// <summary>
    /// Gathers the server and node container logs, each under a labeled section, so
    /// a failed stack start shows what the containers printed.
    /// </summary>
    private async Task<string> CollectLogsAsync()
    {
        var serverLogs = await GetLogsAsync(_server, "server");
        var nodeLogs = await GetLogsAsync(_node, "node");
        return $"--- server container logs ---{Environment.NewLine}{serverLogs}"
            + $"{Environment.NewLine}--- node container logs ---{Environment.NewLine}{nodeLogs}";
    }

    /// <summary>
    /// Reads one container's logs, or an explanation when the container never
    /// started or its logs are unavailable.
    /// </summary>
    private static async Task<string> GetLogsAsync(IContainer? container, string name)
    {
        if (container is null)
        {
            return $"(the {name} container was never started)";
        }

        try
        {
            var (stdout, stderr) = await container.GetLogsAsync();
            return string.IsNullOrEmpty(stdout) && string.IsNullOrEmpty(stderr)
                ? "(no logs)"
                : stdout + Environment.NewLine + stderr;
        }
        catch (Exception exception)
        {
            return $"(could not fetch the {name} logs: {exception.Message})";
        }
    }

    /// <summary>
    /// Builds the named image from one of the repo's Dockerfiles with the repository
    /// root as build context.
    /// </summary>
    private static async Task BuildImageAsync(string repoRoot, string dockerfile, string imageName)
    {
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(repoRoot)
            .WithDockerfile(dockerfile)
            .WithName(imageName)
            .Build();
        await image.CreateAsync();
    }

    /// <summary>
    /// Waits until the API answers its health endpoint (which includes the database
    /// check); the container start only guarantees the port is open, not that the app
    /// is serving. Default timeout: 30s.
    /// </summary>
    private async Task WaitForServerHealthAsync()
    {
        await RetryUntilAsync(async () =>
        {
            using var response = await Api.GetAsync("/v1/health");
            return response.IsSuccessStatusCode;
        }, TimeSpan.FromSeconds(30), "the server's health endpoint to answer");
    }

    /// <summary>
    /// Waits until the enrolled node reports as connected through the API.
    /// Default timeout: 60s.
    /// </summary>
    private async Task WaitForConnectedNodeAsync()
    {
        JsonElement? status = null;
        await RetryUntilAsync(async () =>
        {
            var statuses = await Api.GetFromJsonAsync<JsonElement>("/v1/nodes/status");
            var connected = statuses.EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("isConnected").GetBoolean());
            if (connected.ValueKind == JsonValueKind.Object)
            {
                status = connected;
                return true;
            }

            return false;
        }, TimeSpan.FromSeconds(60), "the node to connect");

        NodeId = status!.Value.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Runs the check on a one-second cadence until it returns true or the deadline
    /// passes, in which case the description names what was being waited for.
    /// </summary>
    public static async Task RetryUntilAsync(Func<Task<bool>> check, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await check())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Timed out waiting for {description}");
    }

    /// <summary>
    /// Walks up from the test assembly to the directory holding Obicon.slnx, so the
    /// fixture can hand the repository root to the image build. Default: throws when
    /// the solution file is not found.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Obicon.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (Obicon.slnx)");
    }
}
