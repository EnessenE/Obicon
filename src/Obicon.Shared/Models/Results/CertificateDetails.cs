namespace Obicon.Shared.Models.Results;

/// <summary>
/// TLS certificate of an HTTPS response.
/// </summary>
public class CertificateDetails
{
    /// <summary>
    /// Subject distinguished name of the certificate. Default: empty string.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Issuer distinguished name of the certificate. Default: empty string.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// UTC date the certificate becomes valid. Default: null.
    /// </summary>
    public DateTime? NotBefore { get; set; }

    /// <summary>
    /// UTC date the certificate expires. Default: null.
    /// </summary>
    public DateTime? NotAfter { get; set; }

    /// <summary>
    /// Subject alternative names (DNS entries) of the certificate. Default: empty list.
    /// </summary>
    public List<string> SubjectAlternativeNames { get; set; } = [];

    /// <summary>
    /// Days remaining until expiry, negative when already expired. Default: null.
    /// </summary>
    public double? DaysRemaining { get; set; }
}
