using System.Security.Cryptography.X509Certificates;
using Obicon.Shared.Models.Results;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Builds the shared certificate details from an X509 certificate.
/// </summary>
public static class CertificateInfo
{
    /// <summary>
    /// Maps a certificate to its details: subject, issuer, validity window, days
    /// remaining, and DNS subject alternative names. Null when the certificate is null.
    /// </summary>
    public static CertificateDetails? ToDetails(X509Certificate2? certificate)
    {
        if (certificate == null)
        {
            return null;
        }

        return new CertificateDetails
        {
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            NotBefore = DateTime.SpecifyKind(certificate.NotBefore, DateTimeKind.Utc),
            NotAfter = DateTime.SpecifyKind(certificate.NotAfter, DateTimeKind.Utc),
            DaysRemaining = Math.Round((certificate.NotAfter - DateTime.UtcNow).TotalDays, 1),
            SubjectAlternativeNames = ParseSubjectAlternativeNames(certificate)
        };
    }

    /// <summary>
    /// Extracts the DNS names from the subject alternative names extension. The
    /// formatted extension reads like "DNS Name=example.com, IP Address=1.2.3.4";
    /// only the DNS entries are kept. Default: empty list.
    /// </summary>
    private static List<string> ParseSubjectAlternativeNames(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions.FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");
        if (extension == null)
        {
            return [];
        }

        // The formatted extension reads like "DNS:example.com, DNS:*.example.com" on
        // Linux and "DNS Name=example.com" on Windows; both shapes are handled
        return extension.Format(false)
            .Split(',')
            .Select(part => part.Trim())
            .Select(part => part.StartsWith("DNS Name=", StringComparison.Ordinal)
                ? part["DNS Name=".Length..]
                : part.StartsWith("DNS:", StringComparison.Ordinal) ? part["DNS:".Length..] : null)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToList();
    }
}
