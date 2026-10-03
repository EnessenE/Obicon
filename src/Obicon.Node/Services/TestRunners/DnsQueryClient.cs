using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Obicon.Shared.Models.Results;

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
    /// Records of every queried type the answer contained. Default: empty list.
    /// </summary>
    public List<DnsRecord> Records { get; init; } = [];

    /// <summary>
    /// Response status per queried type, e.g. "A" => "NOERROR". Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string> Statuses { get; init; } = [];

    /// <summary>
    /// Error description when the query failed. Null on success.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Minimal DNS-over-UDP client. Queries the system's nameservers directly so the result
/// can report which server answered and how long it took. Supports the address records
/// plus CNAME, TXT, MX, and CAA.
/// </summary>
public static class DnsQueryClient
{
    /// <summary>
    /// The supported query types by name and DNS type number. Default: empty.
    /// </summary>
    public static readonly (string Name, ushort Value)[] SupportedQueryTypes =
    [
        ("A", 1),
        ("AAAA", 28),
        ("CNAME", 5),
        ("TXT", 16),
        ("MX", 15),
        ("CAA", 257)
    ];

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
    /// Queries one nameserver for the given record types of a host, sequentially on one
    /// socket. Every answer's records are combined into the result.
    /// </summary>
    public static async Task<NameserverResult> QueryAsync(IPAddress nameserver, string host, IReadOnlyList<(string Name, ushort Value)> queryTypes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(new IPEndPoint(
            nameserver.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var records = new List<DnsRecord>();
            var statuses = new Dictionary<string, string>();
            foreach (var (name, value) in queryTypes)
            {
                var response = await QueryOnceAsync(udp, host, value, timeoutCts.Token);
                records.AddRange(ParseRecords(response));
                statuses[name] = ParseStatus(response);
            }
            stopwatch.Stop();

            return new NameserverResult
            {
                Nameserver = nameserver.ToString(),
                RttMs = stopwatch.Elapsed.TotalMilliseconds,
                Records = records,
                Statuses = statuses
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
    /// Parses the response code of a DNS response into its mnemonic, e.g. "NOERROR".
    /// </summary>
    private static string ParseStatus(byte[] response)
    {
        if (response.Length < 12)
        {
            return string.Empty;
        }

        return (response[3] & 0x0F) switch
        {
            0 => "NOERROR",
            1 => "FORMERR",
            2 => "SERVFAIL",
            3 => "NXDOMAIN",
            4 => "NOTIMP",
            5 => "REFUSED",
            var code => $"RCODE{code}"
        };
    }

    /// <summary>
    /// Parses every answer record of a DNS response, of any supported type.
    /// </summary>
    private static List<DnsRecord> ParseRecords(byte[] response)
    {
        var records = new List<DnsRecord>();
        if (response.Length < 12)
        {
            return records;
        }

        // Skip the question section
        var (_, offset) = ReadName(response, 12);
        offset += 4; // qtype + qclass

        var answerCount = (response[6] << 8) | response[7];
        for (var i = 0; i < answerCount && offset < response.Length; i++)
        {
            (_, offset) = ReadName(response, offset);
            if (offset + 10 > response.Length)
            {
                break;
            }

            var type = (ushort)((response[offset] << 8) | response[offset + 1]);
            var ttl = ((long)response[offset + 4] << 24) | ((long)response[offset + 5] << 16)
                | ((long)response[offset + 6] << 8) | response[offset + 7];
            var dataLength = (response[offset + 8] << 8) | response[offset + 9];
            offset += 10;

            if (dataLength <= 0 || offset + dataLength > response.Length)
            {
                break;
            }

            var value = ParseRecordValue(response, offset, dataLength, type);
            if (value != null)
            {
                records.Add(new DnsRecord { RecordType = TypeName(type), Value = value, TtlSeconds = ttl });
            }

            offset += dataLength;
        }
        return records;
    }

    /// <summary>
    /// Parses one record's value by its type. Null when the type is not supported.
    /// </summary>
    private static string? ParseRecordValue(byte[] response, int offset, int dataLength, ushort type)
    {
        switch (type)
        {
            case 1 when dataLength == 4:
            case 28 when dataLength == 16:
                return new IPAddress(response[offset..(offset + dataLength)]).ToString();

            case 5: // CNAME: a compressed name
            case 2: // NS: same shape, rendered for free
                return ReadName(response, offset).Name;

            case 15 when dataLength >= 3: // MX: 2-byte preference plus the exchange name
                var preference = (response[offset] << 8) | response[offset + 1];
                var (exchange, _) = ReadName(response, offset + 2);
                return $"{preference} {exchange}";

            case 16: // TXT: one or more length-prefixed character-strings
                var text = new StringBuilder();
                var position = offset;
                var end = offset + dataLength;
                while (position < end)
                {
                    var length = response[position];
                    if (position + 1 + length > end)
                    {
                        break;
                    }
                    text.Append(Encoding.ASCII.GetString(response, position + 1, length));
                    position += 1 + length;
                }
                return text.ToString();

            case 257 when dataLength >= 2: // CAA: flags byte, length-prefixed tag, value
                var flags = response[offset];
                var tagLength = response[offset + 1];
                if (offset + 2 + tagLength > offset + dataLength)
                {
                    return null;
                }
                var tag = Encoding.ASCII.GetString(response, offset + 2, tagLength);
                var caaValue = Encoding.ASCII.GetString(response[(offset + 2 + tagLength)..(offset + dataLength)]);
                return $"{flags} {tag} {caaValue}";

            default:
                return null;
        }
    }

    /// <summary>
    /// Maps a DNS type number to its name, e.g. 15 to "MX".
    /// </summary>
    private static string TypeName(ushort type)
    {
        return type switch
        {
            1 => "A",
            28 => "AAAA",
            5 => "CNAME",
            16 => "TXT",
            15 => "MX",
            257 => "CAA",
            var other => $"TYPE{other}"
        };
    }

    /// <summary>
    /// Reads a (possibly compressed) domain name and the offset right after it in the
    /// original stream. Compression pointers are followed with a loop guard.
    /// </summary>
    private static (string Name, int NextOffset) ReadName(byte[] message, int offset)
    {
        var labels = new List<string>();
        var nextOffset = -1;
        var jumps = 0;
        while (offset >= 0 && offset < message.Length)
        {
            var length = message[offset];
            if (length == 0)
            {
                if (nextOffset < 0)
                {
                    nextOffset = offset + 1;
                }
                break;
            }

            if ((length & 0xC0) == 0xC0 && offset + 1 < message.Length)
            {
                if (nextOffset < 0)
                {
                    nextOffset = offset + 2;
                }
                if (++jumps > 16)
                {
                    break;
                }
                offset = ((length & 0x3F) << 8) | message[offset + 1];
                continue;
            }

            if (offset + 1 + length > message.Length)
            {
                break;
            }

            labels.Add(Encoding.ASCII.GetString(message, offset + 1, length));
            offset += length + 1;
        }

        return (string.Join(".", labels), nextOffset < 0 ? offset : nextOffset);
    }
}
