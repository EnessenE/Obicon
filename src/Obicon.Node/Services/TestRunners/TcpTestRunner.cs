using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// TCP connect test. Target format: host or host:port (default port 80).
/// </summary>
public partial class TcpTestRunner : ITestRunner
{
    private const int DefaultPort = 80;
    private readonly ILogger<TcpTestRunner> _logger;

    public TcpTestRunner(ILogger<TcpTestRunner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TestType Type => TestType.Tcp;

    /// <inheritdoc />
    public async Task<TestOutcome> ExecuteAsync(TestAssignmentMessage assignment, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var metrics = new Dictionary<string, object> { ["target"] = assignment.Target };

        var separator = assignment.Target.LastIndexOf(':');
        var host = separator > 0 ? assignment.Target[..separator] : assignment.Target;
        var port = separator > 0 && int.TryParse(assignment.Target[(separator + 1)..], out var parsed) ? parsed : DefaultPort;
        metrics["port"] = port;

        IPAddress? address;
        try
        {
            var dnsStopwatch = Stopwatch.StartNew();
            address = await HostResolver.ResolveAsync(host, assignment.IpVersion, cancellationToken);
            dnsStopwatch.Stop();
            metrics["dns_ms"] = Math.Round(dnsStopwatch.Elapsed.TotalMilliseconds, 2);

            if (address == null)
            {
                LogNoAddress(host, HostResolver.FamilyName(assignment.IpVersion));
                return new TestOutcome
                {
                    Success = false,
                    Output = $"{host} has no {HostResolver.FamilyName(assignment.IpVersion)} address",
                    Metrics = metrics
                };
            }

            metrics["resolved"] = address.ToString();
            LogResolved(host, address, dnsStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException ex)
        {
            return new TestOutcome { Success = false, Output = $"Could not resolve {host}: {ex.SocketErrorCode}", Metrics = metrics };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        var connectStopwatch = Stopwatch.StartNew();
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), timeoutCts.Token);
            connectStopwatch.Stop();
            metrics["connect_ms"] = Math.Round(connectStopwatch.Elapsed.TotalMilliseconds, 2);
            metrics["family"] = address.AddressFamily.ToString();
            LogTcpConnected(host, port, connectStopwatch.Elapsed.TotalMilliseconds);

            return new TestOutcome
            {
                Success = true,
                Output = $"Connected to {host}:{port} ({address.AddressFamily}) in {connectStopwatch.Elapsed.TotalMilliseconds:F0} ms",
                Metrics = metrics
            };
        }
        catch (SocketException ex)
        {
            LogTcpConnectFailed(host, port, ex.SocketErrorCode);
            return new TestOutcome { Success = false, Output = $"TCP connect to {host}:{port} failed: {ex.SocketErrorCode}", Metrics = metrics };
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Host} has no {Family} address")]
    private partial void LogNoAddress(string host, string family);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Host} to {Address} in {DnsMs:F1} ms")]
    private partial void LogResolved(string host, IPAddress address, double dnsMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "TCP connected to {Host}:{Port} in {ConnectMs:F1} ms")]
    private partial void LogTcpConnected(string host, int port, double connectMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TCP connect to {Host}:{Port} failed: {Error}")]
    private partial void LogTcpConnectFailed(string host, int port, SocketError error);
}
