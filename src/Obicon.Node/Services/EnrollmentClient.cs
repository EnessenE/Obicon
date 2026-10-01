using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;

namespace Obicon.Node.Services;

/// <summary>
/// Enrolls this node on the primary server using an enroll token and the
/// NodeAutoEnrollmentEnabled server setting.
/// </summary>
public partial class EnrollmentClient
{
    private static readonly HttpClient Client = new();

    private readonly NodeSettings _settings;
    private readonly NodeIdentityStore _identityStore;
    private readonly ILogger<EnrollmentClient> _logger;

    public EnrollmentClient(IOptions<NodeSettings> settings, NodeIdentityStore identityStore, ILogger<EnrollmentClient> logger)
    {
        _settings = settings.Value;
        _identityStore = identityStore;
        _logger = logger;
    }

    /// <summary>
    /// Enrolls (or updates) this node on the server and returns the auth token to connect with.
    /// </summary>
    public async Task<string> EnrollAsync(CancellationToken cancellationToken)
    {
        var nodeName = string.IsNullOrWhiteSpace(_settings.NodeName)
            ? Environment.MachineName
            : _settings.NodeName;

        // The enroll endpoint lives next to the WebSocket URL
        var baseUri = new Uri(_settings.ServerUrl.TrimEnd('/'));
        var scheme = baseUri.Scheme == "wss" ? "https" : "http";
        var enrollUrl = new Uri($"{scheme}://{baseUri.Authority}/v1/enroll");

        var payload = new
        {
            EnrollToken = _settings.EnrollToken,
            NodeId = _identityStore.NodeId,
            NodeName = nodeName,
            Labels = _settings.Labels,
            Pools = _settings.Pools
        };

        LogEnrolling(nodeName, enrollUrl);

        using var response = await Client.PostAsJsonAsync(enrollUrl, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Enrollment failed with status {response.StatusCode}: {body}");
        }

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var nodeId = document.RootElement.GetProperty("id").GetString()!;
        var authToken = document.RootElement.GetProperty("authToken").GetString()!;

        _identityStore.Save(nodeId, authToken);
        LogEnrolled(nodeId);

        return authToken;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrolling as {NodeName} at {EnrollUrl}")]
    private partial void LogEnrolling(string nodeName, Uri enrollUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrolled as node {NodeId}")]
    private partial void LogEnrolled(string nodeId);
}
