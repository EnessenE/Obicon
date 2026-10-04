using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Obicon.Server.Models;
using Obicon.Server.Services;
using Obicon.Shared.Models.Enums;
using Obicon.Shared.Models.Results;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Round-trip tests for the normalized result details: every test-type section
/// survives the trip through its detail tables and comes back as the shared wire
/// shape, arrays and order included - and the queue API serves it in camelCase.
/// </summary>
public class ResultDetailsTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;
    private readonly ITestQueueService _queue;

    public ResultDetailsTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
        _queue = factory.Services.GetRequiredService<ITestQueueService>();
    }

    /// <summary>
    /// Enqueues a job, completes it with the given details, and returns the stored job.
    /// </summary>
    private async Task<TestJob> CompleteWithDetailsAsync(TestResultDetails details)
    {
        var job = await _queue.EnqueueJobAsync(new TestJob
        {
            TestId = Guid.NewGuid(),
            NodeId = Guid.NewGuid(),
            Target = "example.com",
            TestType = TestType.Ping
        });

        return (await _queue.UpdateJobStatusAsync(job.Id, TestJobStatus.Completed, new TestResult
        {
            Success = true,
            DurationMs = 100,
            Output = "finished",
            Details = details
        }))!;
    }

    [Fact]
    public async Task Traceroute_RoundTrips_WithHopsAndProbes()
    {
        var stored = await CompleteWithDetailsAsync(new TestResultDetails
        {
            Traceroute = new()
            {
                ResolvedAddress = "93.184.216.34",
                TargetReached = true,
                HopCount = 2,
                Hops =
                [
                    new()
                    {
                        Hop = 1,
                        Address = "10.0.0.1",
                        Hostname = "gw.local",
                        Status = "TtlExpired",
                        RoundtripMs = 2.5,
                        Probes =
                        [
                            new() { Status = "TtlExpired", RoundtripMs = 2 },
                            new() { Status = "TtlExpired", RoundtripMs = 3 }
                        ]
                    },
                    new()
                    {
                        Hop = 2,
                        Address = "93.184.216.34",
                        Status = "Success",
                        RoundtripMs = 12.25,
                        Error = null,
                        Probes = [new() { Status = "Success", RoundtripMs = 12.25 }]
                    }
                ]
            }
        });

        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.NotNull(details);
        Assert.NotNull(details.Traceroute);
        Assert.Equal("93.184.216.34", details.Traceroute.ResolvedAddress);
        Assert.True(details.Traceroute.TargetReached);
        Assert.Equal(2, details.Traceroute.HopCount);

        // Hops come back ordered by hop number, probes by their position
        Assert.Equal(2, details.Traceroute.Hops.Count);
        var first = details.Traceroute.Hops[0];
        Assert.Equal(1, first.Hop);
        Assert.Equal("10.0.0.1", first.Address);
        Assert.Equal("gw.local", first.Hostname);
        Assert.Equal("TtlExpired", first.Status);
        Assert.Equal(2.5, first.RoundtripMs);
        Assert.Equal(2, first.Probes.Count);
        Assert.Equal(2, first.Probes[0].RoundtripMs);
        Assert.Equal(3, first.Probes[1].RoundtripMs);

        var second = details.Traceroute.Hops[1];
        Assert.Equal("Success", second.Status);
        Assert.Single(second.Probes);
        Assert.Equal(12.25, second.Probes[0].RoundtripMs);
    }

    [Fact]
    public async Task Ping_RoundTrips_WithRepliesAndStatistics()
    {
        var stored = await CompleteWithDetailsAsync(new TestResultDetails
        {
            Ping = new()
            {
                Target = "example.com",
                ResolvedAddress = "93.184.216.34",
                DnsMs = 1.5,
                ReplyAddress = "93.184.216.34",
                ReplyStatus = "Success",
                RoundtripMs = 11.5,
                Ttl = 56,
                WallclockMs = 4005,
                Sent = 4,
                Received = 3,
                LossPercent = 25,
                MinRoundtripMs = 10,
                AvgRoundtripMs = 11,
                MaxRoundtripMs = 12,
                Error = null,
                Replies =
                [
                    new() { ReplyAddress = "93.184.216.34", ReplyStatus = "Success", RoundtripMs = 10, Ttl = 56 },
                    new() { ReplyStatus = "TimedOut" }
                ]
            }
        });

        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.NotNull(details);
        Assert.NotNull(details.Ping);
        Assert.Equal("example.com", details.Ping.Target);
        Assert.Equal(4, details.Ping.Sent);
        Assert.Equal(3, details.Ping.Received);
        Assert.Equal(25, details.Ping.LossPercent);
        Assert.Equal(10, details.Ping.MinRoundtripMs);
        Assert.Equal(12, details.Ping.MaxRoundtripMs);

        Assert.Equal(2, details.Ping.Replies.Count);
        Assert.Equal("93.184.216.34", details.Ping.Replies[0].ReplyAddress);
        Assert.Equal(10, details.Ping.Replies[0].RoundtripMs);
        Assert.Equal(56, details.Ping.Replies[0].Ttl);
        Assert.Equal("TimedOut", details.Ping.Replies[1].ReplyStatus);
    }

    [Fact]
    public async Task Tcp_RoundTrips_AllScalarFields()
    {
        var stored = await CompleteWithDetailsAsync(new TestResultDetails
        {
            Tcp = new()
            {
                Host = "example.com",
                Port = 443,
                ResolvedAddress = "93.184.216.34",
                Family = "IPv4",
                DnsMs = 3.25,
                ConnectMs = 20.5,
                Error = null
            }
        });

        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.NotNull(details);
        Assert.NotNull(details.Tcp);
        Assert.Equal("example.com", details.Tcp.Host);
        Assert.Equal(443, details.Tcp.Port);
        Assert.Equal("93.184.216.34", details.Tcp.ResolvedAddress);
        Assert.Equal("IPv4", details.Tcp.Family);
        Assert.Equal(3.25, details.Tcp.DnsMs);
        Assert.Equal(20.5, details.Tcp.ConnectMs);
    }

    [Fact]
    public async Task Http_RoundTrips_WithCertificateAndSans()
    {
        var stored = await CompleteWithDetailsAsync(new TestResultDetails
        {
            Http = new()
            {
                Url = "https://example.com/",
                Method = "GET",
                FinalUrl = "https://example.com/final",
                StatusCode = 200,
                ReasonPhrase = "OK",
                ResolvedAddress = "93.184.216.34",
                DnsMs = 1,
                ConnectMs = 2,
                TlsMs = 30.5,
                TlsProtocol = "Tls13",
                TlsCipher = "Tls13Aes128GcmSha256",
                TtfbMs = 40,
                TransferMs = 5,
                BytesRead = 1256,
                BytesTruncated = false,
                ProxyUrl = null,
                BodyMatched = true,
                Error = null,
                Certificate = new()
                {
                    Subject = "CN=example.com",
                    Issuer = "CN=Example Org",
                    NotBefore = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    NotAfter = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    DaysRemaining = 92.5,
                    SubjectAlternativeNames = ["example.com", "www.example.com"]
                }
            }
        });

        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.NotNull(details);
        Assert.NotNull(details.Http);
        Assert.Equal("https://example.com/", details.Http.Url);
        Assert.Equal(200, details.Http.StatusCode);
        Assert.Equal("Tls13", details.Http.TlsProtocol);
        Assert.Equal(1256, details.Http.BytesRead);
        Assert.True(details.Http.BodyMatched);

        Assert.NotNull(details.Http.Certificate);
        Assert.Equal("CN=example.com", details.Http.Certificate.Subject);
        Assert.Equal(92.5, details.Http.Certificate.DaysRemaining);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), details.Http.Certificate.NotBefore);
        Assert.Equal(["example.com", "www.example.com"], details.Http.Certificate.SubjectAlternativeNames);
    }

    [Fact]
    public async Task Dns_RoundTrips_WithRecordsAndArrayColumns()
    {
        var stored = await CompleteWithDetailsAsync(new TestResultDetails
        {
            Dns = new()
            {
                Host = "example.com",
                NameserversQueried = ["1.1.1.1", "8.8.8.8"],
                AnsweringNameserver = "1.1.1.1",
                NameserverRttMs = 12.5,
                Records =
                [
                    new() { RecordType = "A", Value = "93.184.216.34", TtlSeconds = 300 },
                    new() { RecordType = "CNAME", Value = "edge.example.net", TtlSeconds = 600 }
                ],
                Resolved = ["93.184.216.34"],
                Via = "nameserver",
                ResponseStatus = "NOERROR",
                QueryType = "A",
                ExpectedAddress = "93.184.216.34",
                ExpectedMatched = true,
                Error = null
            }
        });

        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.NotNull(details);
        Assert.NotNull(details.Dns);
        Assert.Equal("example.com", details.Dns.Host);
        Assert.Equal(["1.1.1.1", "8.8.8.8"], details.Dns.NameserversQueried);
        Assert.Equal(["93.184.216.34"], details.Dns.Resolved);
        Assert.Equal("NOERROR", details.Dns.ResponseStatus);
        Assert.True(details.Dns.ExpectedMatched);

        Assert.Equal(2, details.Dns.Records.Count);
        Assert.Equal("A", details.Dns.Records[0].RecordType);
        Assert.Equal(300, details.Dns.Records[0].TtlSeconds);
        Assert.Equal("CNAME", details.Dns.Records[1].RecordType);
        Assert.Equal("edge.example.net", details.Dns.Records[1].Value);
    }

    [Fact]
    public async Task Tls_RoundTrips_WithCertificate()
    {
        var stored = await CompleteWithDetailsAsync(new TestResultDetails
        {
            Tls = new()
            {
                Host = "example.com",
                Port = 443,
                ResolvedAddress = "93.184.216.34",
                Family = "IPv6",
                DnsMs = 4,
                ConnectMs = 8,
                HandshakeMs = 21.5,
                Protocol = "Tls13",
                Cipher = "Tls13Aes256GcmSha384",
                Error = null,
                Certificate = new()
                {
                    Subject = "CN=example.com",
                    Issuer = "CN=Example Org",
                    DaysRemaining = 10,
                    SubjectAlternativeNames = []
                }
            }
        });

        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.NotNull(details);
        Assert.NotNull(details.Tls);
        Assert.Equal(443, details.Tls.Port);
        Assert.Equal(21.5, details.Tls.HandshakeMs);
        Assert.Equal("Tls13Aes256GcmSha384", details.Tls.Cipher);

        Assert.NotNull(details.Tls.Certificate);
        Assert.Equal("CN=Example Org", details.Tls.Certificate.Issuer);
        Assert.Equal(10, details.Tls.Certificate.DaysRemaining);
    }

    [Fact]
    public async Task Details_AreNullWhenNoSectionWasReported()
    {
        var stored = await CompleteWithDetailsAsync(new TestResultDetails());

        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.Null(details);
    }

    [Fact]
    public async Task QueueApi_ServesTheDetailsInCamelCase()
    {
        var job = await _queue.EnqueueJobAsync(new TestJob
        {
            TestId = Guid.NewGuid(),
            NodeId = Guid.NewGuid(),
            Target = "example.com",
            TestType = TestType.Traceroute
        });
        await _queue.UpdateJobStatusAsync(job.Id, TestJobStatus.Completed, new TestResult
        {
            Success = true,
            DurationMs = 88,
            Output = "done",
            Details = new()
            {
                Traceroute = new()
                {
                    ResolvedAddress = "93.184.216.34",
                    TargetReached = true,
                    HopCount = 1,
                    Hops = [new() { Hop = 1, Address = "93.184.216.34", Status = "Success" }]
                }
            }
        });

        var response = await _client.GetFromJsonAsync<JsonElement>($"/v1/queue/{job.Id}");
        Assert.True(response.GetProperty("success").GetBoolean());
        Assert.Equal(88, response.GetProperty("durationMs").GetInt64());

        var traceroute = response.GetProperty("details").GetProperty("traceroute");
        Assert.Equal("93.184.216.34", traceroute.GetProperty("resolvedAddress").GetString());
        var hop = traceroute.GetProperty("hops")[0];
        Assert.Equal(1, hop.GetProperty("hop").GetInt32());
        Assert.Equal("93.184.216.34", hop.GetProperty("address").GetString());
    }
}
