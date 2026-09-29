using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Result of querying one nameserver.
/// </summary>
public class NameserverResult
{
    /// <summary>
    /// Nameserver that was queried.
    /// </summary>
    public string Nameserver { get; init; } = string.Empty;

    /// <summary>
    /// Round-trip time of the queries in milliseconds. -1 when the query failed.
    /// </summary>
    public double RttMs { get; init; } = -1;

    /// <summary>
    /// A records returned by this nameserver. Default: empty list.
    /// </summary>
    public List<string> A { get; init; } = new();

    /// <summary>
    /// AAAA records returned by this nameserver. Default: empty list.
    /// </summary>
    public List<string> Aaaa { get; init; } = new();

    /// <summary>
    /// Error description when the query failed. Null on success.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Minimal DNS-over-UDP client. Queries the system's nameservers directly so the result
/// can report which server answered and how long it took.
/// </summary>
public static class DnsQueryClient
{
    /// <summary>
    /// Reads the system's nameservers, e.g. from /etc/resolv.conf. Default: empty list.
    /// </summary>
    public static List<IPAddress> GetSystemNameservers()
    {
        var nameservers = new List<IPAddress>();
        try
        {
            if (!File.Exists("/etc/resolv.conf"))
            {
                return nameservers;
            }

            foreach (var line in File.ReadAllLines("/etc/resolv.conf"))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("nameserver", StringComparison.Ordinal) &&
                    IPAddress.TryParse(trimmed["nameserver".Length..].Trim(), out var server))
                {
                    nameservers.Add(server);
                }
            }
        }
        catch (Exception)
        {
            // Best effort: callers fall back to the OS resolver
        }
        return nameservers;
    }

    /// <summary>
    /// Queries one nameserver for A and AAAA records of a host, sequentially on one socket.
    /// </summary>
    public static async Task<NameserverResult> QueryAsync(IPAddress nameserver, string host, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(new IPEndPoint(
            nameserver.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var aResponse = await QueryOnceAsync(udp, host, 1, timeoutCts.Token);
            var aaaaResponse = await QueryOnceAsync(udp, host, 28, timeoutCts.Token);
            stopwatch.Stop();

            return new NameserverResult
            {
                Nameserver = nameserver.ToString(),
                RttMs = stopwatch.Elapsed.TotalMilliseconds,
                A = ParseRecords(aResponse, 1),
                Aaaa = ParseRecords(aaaaResponse, 28)
            };
        }
        catch (Exception ex)
        {
            return new NameserverResult
            {
                Nameserver = nameserver.ToString(),
                RttMs = -1,
                Error = ex is OperationCanceledException ? "timeout" : ex.Message
            };
        }
    }

    /// <summary>
    /// Sends one query and returns the response with the matching transaction id,
    /// ignoring responses for other transaction ids.
    /// </summary>
    private static async Task<byte[]> QueryOnceAsync(UdpClient udp, string host, ushort qtype, CancellationToken ct)
    {
        var id = (ushort)Random.Shared.Next(0x0001, 0xFFFF);
        var query = BuildQuery(id, host, qtype);
        await udp.SendAsync(query, ct);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var received = await udp.ReceiveAsync(ct);
            if (received.Buffer.Length >= 2 && BitConverter.ToUInt16(received.Buffer, 0) == id)
            {
                return received.Buffer;
            }
        }
        throw new TimeoutException("No response with a matching transaction id");
    }

    private static byte[] BuildQuery(ushort id, string host, ushort qtype)
    {
        using var message = new MemoryStream();
        message.Write([(byte)(id >> 8), (byte)(id & 0xFF), 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        foreach (var label in host.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(label);
            message.WriteByte((byte)bytes.Length);
            message.Write(bytes);
        }
        message.WriteByte(0);
        message.Write([(byte)(qtype >> 8), (byte)(qtype & 0xFF), 0x00, 0x01]);
        return message.ToArray();
    }

    /// <summary>
    /// Parses A (1) or AAAA (28) records from a DNS response.
    /// </summary>
    private static List<string> ParseRecords(byte[] response, ushort wantedType)
    {
        var records = new List<string>();
        if (response.Length < 12)
        {
            return records;
        }

        var offset = 12;
        while (offset < response.Length && response[offset] != 0)
        {
            offset += response[offset] + 1;
        }
        offset += 5; // null label + qtype + qclass

        var answerCount = (response[6] << 8) | response[7];
        for (var i = 0; i < answerCount && offset + 12 <= response.Length; i++)
        {
            offset = SkipName(response, offset);
            if (offset + 10 > response.Length)
            {
                break;
            }

            var type = (ushort)((response[offset] << 8) | response[offset + 1]);
            var dataLength = (response[offset + 8] << 8) | response[offset + 9];
            offset += 10;

            if (type == wantedType && dataLength > 0 && offset + dataLength <= response.Length)
            {
                var data = response[offset..(offset + dataLength)];
                if (wantedType == 1 && dataLength == 4)
                {
                    records.Add(new IPAddress(data).ToString());
                }
                else if (wantedType == 28 && dataLength == 16)
                {
                    records.Add(new IPAddress(data).ToString());
                }
            }

            offset += dataLength;
        }
        return records;
    }

    private static int SkipName(byte[] message, int offset)
    {
        while (offset < message.Length)
        {
            var length = message[offset];
            if ((length & 0xC0) == 0xC0)
            {
                return offset + 2;
            }
            if (length == 0)
            {
                return offset + 1;
            }
            offset += length + 1;
        }
        return offset;
    }
}
