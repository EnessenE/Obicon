using Obicon.Server.Data;
using Obicon.Shared.Models.Results;

namespace Obicon.Server.Models;

/// <summary>
/// Maps the shared <see cref="TestResultDetails"/> (the wire and API shape) to and from
/// the normalized detail entities. Persist writes the section rows for exactly one
/// populated section; ToShared reconstructs from a fully loaded job.
/// </summary>
public static class TestResultDetailsMapper
{
    /// <summary>
    /// Adds the detail rows for a finished run to the given context. Callers only ever
    /// pass details with one populated section; every extra section is persisted anyway.
    /// </summary>
    public static void Persist(ObiconDbContext db, Guid jobId, TestResultDetails details)
    {
        if (details.Traceroute is { } traceroute)
        {
            db.TestJobTracerouteDetails.Add(new TestJobTracerouteDetails
            {
                JobId = jobId,
                ResolvedAddress = traceroute.ResolvedAddress,
                TargetReached = traceroute.TargetReached,
                HopCount = traceroute.HopCount
            });
            foreach (var hop in traceroute.Hops.Select((h, i) => (h, i)))
            {
                var hopId = Guid.NewGuid();
                db.TestJobTracerouteHops.Add(new TestJobTracerouteHop
                {
                    Id = hopId,
                    JobId = jobId,
                    Hop = hop.h.Hop,
                    Address = hop.h.Address,
                    Hostname = hop.h.Hostname,
                    Status = hop.h.Status,
                    RoundtripMs = hop.h.RoundtripMs,
                    Error = hop.h.Error
                });
                foreach (var probe in hop.h.Probes.Select((p, i) => (p, i)))
                {
                    db.TestJobTracerouteProbes.Add(new TestJobTracerouteProbe
                    {
                        HopId = hopId,
                        Ordinal = probe.i,
                        Status = probe.p.Status,
                        RoundtripMs = probe.p.RoundtripMs
                    });
                }
            }
        }

        if (details.Ping is { } ping)
        {
            db.TestJobPingDetails.Add(new TestJobPingDetails
            {
                JobId = jobId,
                Target = ping.Target,
                ResolvedAddress = ping.ResolvedAddress,
                DnsMs = ping.DnsMs,
                ReplyAddress = ping.ReplyAddress,
                ReplyStatus = ping.ReplyStatus,
                RoundtripMs = ping.RoundtripMs,
                Ttl = ping.Ttl,
                WallclockMs = ping.WallclockMs,
                Sent = ping.Sent,
                Received = ping.Received,
                LossPercent = ping.LossPercent,
                MinRoundtripMs = ping.MinRoundtripMs,
                AvgRoundtripMs = ping.AvgRoundtripMs,
                MaxRoundtripMs = ping.MaxRoundtripMs,
                Error = ping.Error
            });
            foreach (var reply in ping.Replies.Select((r, i) => (r, i)))
            {
                db.TestJobPingReplies.Add(new TestJobPingReply
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    Ordinal = reply.i,
                    ReplyAddress = reply.r.ReplyAddress,
                    ReplyStatus = reply.r.ReplyStatus,
                    RoundtripMs = reply.r.RoundtripMs,
                    Ttl = reply.r.Ttl
                });
            }
        }

        if (details.Tcp is { } tcp)
        {
            db.TestJobTcpDetails.Add(new TestJobTcpDetails
            {
                JobId = jobId,
                Host = tcp.Host,
                Port = tcp.Port,
                ResolvedAddress = tcp.ResolvedAddress,
                Family = tcp.Family,
                DnsMs = tcp.DnsMs,
                ConnectMs = tcp.ConnectMs,
                Error = tcp.Error
            });
        }

        if (details.Http is { } http)
        {
            db.TestJobHttpDetails.Add(new TestJobHttpDetails
            {
                JobId = jobId,
                Url = http.Url,
                Method = http.Method,
                FinalUrl = http.FinalUrl,
                StatusCode = http.StatusCode,
                ReasonPhrase = http.ReasonPhrase,
                ResolvedAddress = http.ResolvedAddress,
                DnsMs = http.DnsMs,
                ConnectMs = http.ConnectMs,
                TlsMs = http.TlsMs,
                TlsProtocol = http.TlsProtocol,
                TlsCipher = http.TlsCipher,
                TtfbMs = http.TtfbMs,
                TransferMs = http.TransferMs,
                BytesRead = http.BytesRead,
                BytesTruncated = http.BytesTruncated,
                ProxyUrl = http.ProxyUrl,
                BodyMatched = http.BodyMatched,
                Error = http.Error
            });
            PersistCertificate(db, jobId, http.Certificate);
        }

        if (details.Dns is { } dns)
        {
            db.TestJobDnsDetails.Add(new TestJobDnsDetails
            {
                JobId = jobId,
                Host = dns.Host,
                NameserversQueried = dns.NameserversQueried,
                AnsweringNameserver = dns.AnsweringNameserver,
                NameserverRttMs = dns.NameserverRttMs,
                Resolved = dns.Resolved,
                Via = dns.Via,
                ResponseStatus = dns.ResponseStatus,
                QueryType = dns.QueryType,
                ExpectedAddress = dns.ExpectedAddress,
                ExpectedMatched = dns.ExpectedMatched,
                Error = dns.Error
            });
            foreach (var record in dns.Records.Select((r, i) => (r, i)))
            {
                db.TestJobDnsRecords.Add(new TestJobDnsRecord
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    Ordinal = record.i,
                    RecordType = record.r.RecordType,
                    Value = record.r.Value,
                    TtlSeconds = record.r.TtlSeconds
                });
            }
        }

        if (details.Tls is { } tls)
        {
            db.TestJobTlsDetails.Add(new TestJobTlsDetails
            {
                JobId = jobId,
                Host = tls.Host,
                Port = tls.Port,
                ResolvedAddress = tls.ResolvedAddress,
                Family = tls.Family,
                DnsMs = tls.DnsMs,
                ConnectMs = tls.ConnectMs,
                HandshakeMs = tls.HandshakeMs,
                Protocol = tls.Protocol,
                Cipher = tls.Cipher,
                Error = tls.Error
            });
            PersistCertificate(db, jobId, tls.Certificate);
        }
    }

    /// <summary>
    /// Adds the certificate row when the section carries one.
    /// </summary>
    private static void PersistCertificate(ObiconDbContext db, Guid jobId, CertificateDetails? certificate)
    {
        if (certificate == null)
        {
            return;
        }

        db.TestJobCertificates.Add(new TestJobCertificate
        {
            JobId = jobId,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            NotBefore = certificate.NotBefore,
            NotAfter = certificate.NotAfter,
            DaysRemaining = certificate.DaysRemaining,
            SubjectAlternativeNames = certificate.SubjectAlternativeNames
        });
    }

    /// <summary>
    /// Reconstructs the shared details from a job whose detail navigations are loaded.
    /// Null when the job carries no section rows (not finished, or payload stripped).
    /// </summary>
    public static TestResultDetails? ToShared(TestJob job)
    {
        var certificate = job.Certificate == null ? null : new CertificateDetails
        {
            Subject = job.Certificate.Subject,
            Issuer = job.Certificate.Issuer,
            NotBefore = job.Certificate.NotBefore,
            NotAfter = job.Certificate.NotAfter,
            DaysRemaining = job.Certificate.DaysRemaining,
            SubjectAlternativeNames = job.Certificate.SubjectAlternativeNames
        };

        var details = new TestResultDetails
        {
            Traceroute = job.Traceroute == null ? null : new TracerouteDetails
            {
                ResolvedAddress = job.Traceroute.ResolvedAddress,
                TargetReached = job.Traceroute.TargetReached,
                HopCount = job.Traceroute.HopCount,
                Hops = job.Traceroute.Hops
                    .OrderBy(h => h.Hop)
                    .Select(h => new TracerouteHop
                    {
                        Hop = h.Hop,
                        Address = h.Address,
                        Hostname = h.Hostname,
                        Status = h.Status,
                        RoundtripMs = h.RoundtripMs,
                        Error = h.Error,
                        Probes = h.Probes
                            .OrderBy(p => p.Ordinal)
                            .Select(p => new TracerouteProbe { Status = p.Status, RoundtripMs = p.RoundtripMs })
                            .ToList()
                    })
                    .ToList()
            },
            Ping = job.Ping == null ? null : new PingDetails
            {
                Target = job.Ping.Target,
                ResolvedAddress = job.Ping.ResolvedAddress,
                DnsMs = job.Ping.DnsMs,
                ReplyAddress = job.Ping.ReplyAddress,
                ReplyStatus = job.Ping.ReplyStatus,
                RoundtripMs = job.Ping.RoundtripMs,
                Ttl = job.Ping.Ttl,
                WallclockMs = job.Ping.WallclockMs,
                Sent = job.Ping.Sent,
                Received = job.Ping.Received,
                LossPercent = job.Ping.LossPercent,
                MinRoundtripMs = job.Ping.MinRoundtripMs,
                AvgRoundtripMs = job.Ping.AvgRoundtripMs,
                MaxRoundtripMs = job.Ping.MaxRoundtripMs,
                Error = job.Ping.Error,
                Replies = job.Ping.Replies
                    .OrderBy(r => r.Ordinal)
                    .Select(r => new PingReply
                    {
                        ReplyAddress = r.ReplyAddress,
                        ReplyStatus = r.ReplyStatus,
                        RoundtripMs = r.RoundtripMs,
                        Ttl = r.Ttl
                    })
                    .ToList()
            },
            Tcp = job.Tcp == null ? null : new TcpDetails
            {
                Host = job.Tcp.Host,
                Port = job.Tcp.Port,
                ResolvedAddress = job.Tcp.ResolvedAddress,
                Family = job.Tcp.Family,
                DnsMs = job.Tcp.DnsMs,
                ConnectMs = job.Tcp.ConnectMs,
                Error = job.Tcp.Error
            },
            Http = job.Http == null ? null : new HttpDetails
            {
                Url = job.Http.Url,
                Method = job.Http.Method,
                FinalUrl = job.Http.FinalUrl,
                StatusCode = job.Http.StatusCode,
                ReasonPhrase = job.Http.ReasonPhrase,
                ResolvedAddress = job.Http.ResolvedAddress,
                DnsMs = job.Http.DnsMs,
                ConnectMs = job.Http.ConnectMs,
                TlsMs = job.Http.TlsMs,
                TlsProtocol = job.Http.TlsProtocol,
                TlsCipher = job.Http.TlsCipher,
                TtfbMs = job.Http.TtfbMs,
                TransferMs = job.Http.TransferMs,
                BytesRead = job.Http.BytesRead,
                BytesTruncated = job.Http.BytesTruncated,
                ProxyUrl = job.Http.ProxyUrl,
                BodyMatched = job.Http.BodyMatched,
                Error = job.Http.Error,
                Certificate = certificate
            },
            Dns = job.Dns == null ? null : new DnsDetails
            {
                Host = job.Dns.Host,
                NameserversQueried = job.Dns.NameserversQueried,
                AnsweringNameserver = job.Dns.AnsweringNameserver,
                NameserverRttMs = job.Dns.NameserverRttMs,
                Resolved = job.Dns.Resolved,
                Via = job.Dns.Via,
                ResponseStatus = job.Dns.ResponseStatus,
                QueryType = job.Dns.QueryType,
                ExpectedAddress = job.Dns.ExpectedAddress,
                ExpectedMatched = job.Dns.ExpectedMatched,
                Error = job.Dns.Error,
                Records = job.Dns.Records
                    .OrderBy(r => r.Ordinal)
                    .Select(r => new DnsRecord { RecordType = r.RecordType, Value = r.Value, TtlSeconds = r.TtlSeconds })
                    .ToList()
            },
            Tls = job.Tls == null ? null : new TlsDetails
            {
                Host = job.Tls.Host,
                Port = job.Tls.Port,
                ResolvedAddress = job.Tls.ResolvedAddress,
                Family = job.Tls.Family,
                DnsMs = job.Tls.DnsMs,
                ConnectMs = job.Tls.ConnectMs,
                HandshakeMs = job.Tls.HandshakeMs,
                Protocol = job.Tls.Protocol,
                Cipher = job.Tls.Cipher,
                Error = job.Tls.Error,
                Certificate = certificate
            }
        };

        return details.Traceroute == null &&
               details.Ping == null &&
               details.Tcp == null &&
               details.Http == null &&
               details.Dns == null &&
               details.Tls == null
            ? null
            : details;
    }
}
