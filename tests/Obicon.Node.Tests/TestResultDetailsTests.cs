using System.Text.Json;
using Obicon.Shared.Models.Messages;
using Obicon.Shared.Models.Results;
using Xunit;

namespace Obicon.Node.Tests;

/// <summary>
/// Wire-contract tests for the structured result details: the WebSocket JSON must
/// carry the PascalCase section names and round-trip every section intact, and
/// messages from nodes that predate details must still parse.
/// </summary>
public class TestResultDetailsTests
{
    [Fact]
    public void TestResultMessage_Details_RoundTrip_Preserves_All_Sections()
    {
        var message = new TestResultMessage
        {
            JobId = "job-1",
            TestId = "test-1",
            NodeId = "node-1",
            Success = true,
            DurationMs = 42,
            Details = new TestResultDetails
            {
                Traceroute = new TracerouteDetails
                {
                    ResolvedAddress = "93.184.216.34",
                    TargetReached = true,
                    HopCount = 2,
                    Hops =
                    [
                        new TracerouteHop
                        {
                            Hop = 1,
                            Address = "10.0.0.1",
                            Hostname = "router1.local",
                            Status = "TtlExpired",
                            RoundtripMs = 5,
                            Probes =
                            [
                                new TracerouteProbe { Status = "TtlExpired", RoundtripMs = 4 },
                                new TracerouteProbe { Status = "TtlExpired", RoundtripMs = 6 }
                            ]
                        },
                        new TracerouteHop
                        {
                            Hop = 2,
                            Status = "TimedOut",
                            Error = "TimedOut",
                            Probes = [new TracerouteProbe { Status = "TimedOut" }]
                        }
                    ]
                },
                Ping = new PingDetails
                {
                    Target = "example.com",
                    ResolvedAddress = "93.184.216.34",
                    DnsMs = 1.5,
                    ReplyAddress = "93.184.216.34",
                    ReplyStatus = "Success",
                    RoundtripMs = 12,
                    Ttl = 57,
                    WallclockMs = 13.5,
                    Sent = 4,
                    Received = 3,
                    LossPercent = 25,
                    MinRoundtripMs = 10,
                    MaxRoundtripMs = 14,
                    AvgRoundtripMs = 12,
                    Replies =
                    [
                        new PingReply { ReplyAddress = "93.184.216.34", ReplyStatus = "Success", RoundtripMs = 10, Ttl = 57 },
                        new PingReply { ReplyStatus = "TimedOut" }
                    ]
                },
                Tcp = new TcpDetails
                {
                    Host = "example.com",
                    Port = 443,
                    ResolvedAddress = "93.184.216.34",
                    Family = "InterNetwork",
                    DnsMs = 2.5,
                    ConnectMs = 30.25
                },
                Http = new HttpDetails
                {
                    Url = "https://example.com",
                    Method = "HEAD",
                    FinalUrl = "https://example.com/",
                    StatusCode = 200,
                    ReasonPhrase = "OK",
                    ResolvedAddress = "93.184.216.34",
                    DnsMs = 1,
                    ConnectMs = 2,
                    TlsMs = 3,
                    TlsProtocol = "Tls13",
                    TlsCipher = "Tls13Aes128GcmSha256",
                    TtfbMs = 40,
                    TransferMs = 5,
                    BytesRead = 1024,
                    BytesTruncated = false,
                    BodyMatched = true,
                    Certificate = new CertificateDetails
                    {
                        Subject = "CN=example.com",
                        Issuer = "CN=ca",
                        NotAfter = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        DaysRemaining = 100.5
                    }
                },
                Dns = new DnsDetails
                {
                    Host = "example.com",
                    NameserversQueried = ["8.8.8.8"],
                    AnsweringNameserver = "8.8.8.8",
                    NameserverRttMs = 12.5,
                    ARecords = ["93.184.216.34"],
                    AaaaRecords = [],
                    Resolved = ["93.184.216.34"],
                    Via = "nameserver",
                    ResponseStatus = "NOERROR",
                    RecordTtls = new Dictionary<string, long> { ["93.184.216.34"] = 3600 },
                    ExpectedAddress = "93.184.216.34",
                    ExpectedMatched = true
                }
            }
        };

        var json = JsonSerializer.Serialize(message);

        // The wire contract is PascalCase on every section and nested property
        Assert.Contains("\"Details\"", json);
        Assert.Contains("\"Traceroute\"", json);
        Assert.Contains("\"TargetReached\"", json);
        Assert.Contains("\"Ping\"", json);
        Assert.Contains("\"Tcp\"", json);
        Assert.Contains("\"Http\"", json);
        Assert.Contains("\"Certificate\"", json);
        Assert.Contains("\"Dns\"", json);

        var parsed = JsonSerializer.Deserialize<TestResultMessage>(json)!;
        var details = Assert.IsType<TestResultDetails>(parsed.Details);

        var traceroute = Assert.IsType<TracerouteDetails>(details.Traceroute);
        Assert.True(traceroute.TargetReached);
        Assert.Equal(2, traceroute.HopCount);
        var hop = Assert.Single(traceroute.Hops, h => h.Address == null);
        Assert.Equal("TimedOut", hop.Status);
        Assert.Null(hop.RoundtripMs);
        var answeredHop = Assert.Single(traceroute.Hops, h => h.Address != null);
        Assert.Equal("router1.local", answeredHop.Hostname);
        Assert.Equal(2, answeredHop.Probes.Count);
        Assert.Equal(4, Assert.Single(answeredHop.Probes, p => p.RoundtripMs == 4).RoundtripMs);

        var ping = Assert.IsType<PingDetails>(details.Ping);
        Assert.Equal("Success", ping.ReplyStatus);
        Assert.Equal(57, ping.Ttl);
        Assert.Equal(4, ping.Sent);
        Assert.Equal(3, ping.Received);
        Assert.Equal(25, ping.LossPercent);
        Assert.Equal(12, ping.AvgRoundtripMs);
        Assert.Equal(2, ping.Replies.Count);
        Assert.Null(Assert.Single(ping.Replies, r => r.ReplyStatus == "TimedOut").RoundtripMs);

        var tcp = Assert.IsType<TcpDetails>(details.Tcp);
        Assert.Equal(443, tcp.Port);
        Assert.Equal(30.25, tcp.ConnectMs);

        var http = Assert.IsType<HttpDetails>(details.Http);
        Assert.Equal("HEAD", http.Method);
        Assert.Equal(200, http.StatusCode);
        Assert.True(http.BodyMatched);
        var certificate = Assert.IsType<CertificateDetails>(http.Certificate);
        Assert.Equal("CN=example.com", certificate.Subject);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), certificate.NotAfter);

        var dns = Assert.IsType<DnsDetails>(details.Dns);
        Assert.Equal("8.8.8.8", Assert.Single(dns.NameserversQueried));
        Assert.Equal("93.184.216.34", Assert.Single(dns.Resolved));
        Assert.True(dns.ExpectedMatched);
        Assert.Equal("NOERROR", dns.ResponseStatus);
        Assert.Equal(3600, dns.RecordTtls["93.184.216.34"]);
    }

    [Fact]
    public void TestResultMessage_Without_Details_Still_Parses()
    {
        // Messages from nodes older than 0.4.0 carry no Details and must stay valid
        const string json = """{"JobId":"job-1","TestId":"test-1","NodeId":"node-1","Success":true,"DurationMs":7,"Output":"ok","Metrics":{"a":"b"}}""";

        var parsed = JsonSerializer.Deserialize<TestResultMessage>(json)!;

        Assert.True(parsed.Success);
        Assert.Null(parsed.Details);
    }
}
