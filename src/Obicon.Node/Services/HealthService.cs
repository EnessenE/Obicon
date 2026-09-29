using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Node.Models;

namespace Obicon.Node.Services;

/// <summary>
/// Exposes a /health endpoint over HttpListener without pulling in a web server,
/// keeping the node a light console app.
/// </summary>
public class HealthService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly NodeSettings _settings;
    private readonly IServerConnection _serverConnection;
    private readonly ITestExecutor _testExecutor;
    private readonly ILogger<HealthService> _logger;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public HealthService(
        IOptions<NodeSettings> settings,
        IServerConnection serverConnection,
        ITestExecutor testExecutor,
        ILogger<HealthService> logger)
    {
        _settings = settings.Value;
        _serverConnection = serverConnection;
        _testExecutor = testExecutor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var prefix = _settings.HealthUrlPrefix;
        if (!prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        using var registration = stoppingToken.Register(() => listener.Stop());
        try
        {
            listener.Start();
            _logger.LogInformation("Health endpoint listening on {Prefix}health", prefix);
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "Failed to start health endpoint on {Prefix}", prefix);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException ex)
            {
                _logger.LogWarning(ex, "Health endpoint stopped accepting requests");
                break;
            }

            try
            {
                Respond(context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to respond to health request");
            }
        }

        _logger.LogInformation("Health endpoint stopped");
    }

    private void Respond(HttpListenerContext context)
    {
        var isHealthPath = context.Request.Url?.AbsolutePath.TrimEnd('/').EndsWith("/health", StringComparison.OrdinalIgnoreCase)
                           ?? false;
        context.Response.StatusCode = isHealthPath ? 200 : 404;
        context.Response.ContentType = "application/json";

        if (isHealthPath)
        {
            var stats = _testExecutor.Statistics;
            var payload = new HealthPayload
            {
                ConnectedToServer = _serverConnection.IsConnected,
                RunningTests = stats.Running,
                PendingTests = stats.Pending,
                CompletedTests = stats.Completed,
                FailedTests = stats.Failed,
                TimedOutTests = stats.TimedOut,
                Uptime = DateTimeOffset.UtcNow - _startedAt,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes("{\"error\":\"not found\"}");
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        context.Response.Close();
    }
}
