using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services;

/// <summary>
/// Periodically refreshes the node's own addresses and reports changes to the server:
/// the internal (LAN) IPv4 and IPv6 addresses from the network interfaces, and the
/// external (public) IPv4 and IPv6 addresses by asking the configured check services.
/// Values also travel with every registration, so a reconnect always carries current
/// addresses. The external checks are best effort: when one fails (e.g. no IPv6
/// connectivity), that family stays unavailable and the last known value is kept.
/// </summary>
public class IpAddressMonitor : BackgroundService
{
    private readonly NodeSettings _settings;
    private readonly IServerConnection _connection;
    private readonly NodeAddressState _addressState;
    private readonly ILogger<IpAddressMonitor> _logger;
    private readonly HttpClient _httpClient;

    public IpAddressMonitor(
        IOptions<NodeSettings> settings,
        IServerConnection connection,
        NodeAddressState addressState,
        ILogger<IpAddressMonitor> logger)
    {
        _settings = settings.Value;
        _connection = connection;
        _addressState = addressState;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.IpCheckIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            var changed = false;

            var internalIpv4 = IpResolver.GetInternalIpv4();
            var internalIpv6 = IpResolver.GetInternalIpv6();
            var externalIpv4 = await TryGetExternalAsync(_settings.ExternalIpCheckUrl, IpResolver.ParseExternalIpv4, stoppingToken);
            var externalIpv6 = await TryGetExternalAsync(_settings.ExternalIpCheckUrlIpv6, IpResolver.ParseExternalIpv6, stoppingToken);

            if (_addressState.InternalIpv4 != internalIpv4)
            {
                LogChange("Internal IPv4", internalIpv4, _addressState.InternalIpv4);
                _addressState.InternalIpv4 = internalIpv4;
                changed = true;
            }
            if (_addressState.InternalIpv6 != internalIpv6)
            {
                LogChange("Internal IPv6", internalIpv6, _addressState.InternalIpv6);
                _addressState.InternalIpv6 = internalIpv6;
                changed = true;
            }
            if (externalIpv4 != null && externalIpv4 != _addressState.ExternalIpv4)
            {
                LogChange("External IPv4", externalIpv4, _addressState.ExternalIpv4);
                _addressState.ExternalIpv4 = externalIpv4;
                changed = true;
            }
            if (externalIpv6 != null && externalIpv6 != _addressState.ExternalIpv6)
            {
                LogChange("External IPv6", externalIpv6, _addressState.ExternalIpv6);
                _addressState.ExternalIpv6 = externalIpv6;
                changed = true;
            }

            if (changed)
            {
                await SendUpdateAsync();
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void LogChange(string label, string? newValue, string? oldValue)
    {
        _logger.LogInformation("{Label} changed: {New} (was {Old})",
            label, newValue ?? "unavailable", oldValue ?? "unavailable");
    }

    /// <summary>
    /// Asks a check service for the node's public address of one family. Returns null on
    /// any failure; the caller keeps the last known value for that family.
    /// </summary>
    private async Task<string?> TryGetExternalAsync(
        string? url,
        Func<string?, string?> parse,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var body = await _httpClient.GetStringAsync(url, cancellationToken);
            var parsed = parse(body);
            if (parsed == null)
            {
                _logger.LogDebug("External IP check at {Url} returned no address: {Body}", url, body.Trim());
            }
            return parsed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "External IP check at {Url} failed; keeping the last known value", url);
            return null;
        }
    }

    private async Task SendUpdateAsync()
    {
        await _connection.SendAsync(new WebSocketMessage
        {
            Type = MessageType.NodeInfoUpdate,
            Data = new NodeInfoUpdateMessage
            {
                InternalIpv4 = _addressState.InternalIpv4,
                InternalIpv6 = _addressState.InternalIpv6,
                ExternalIpv4 = _addressState.ExternalIpv4,
                ExternalIpv6 = _addressState.ExternalIpv6
            }
        });
    }
}
