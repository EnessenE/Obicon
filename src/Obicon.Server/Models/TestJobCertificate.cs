namespace Obicon.Server.Models;

/// <summary>
/// The TLS certificate of a finished HTTP(S) or TLS run. One row per job that
/// reported a certificate; keyed by the job ID.
/// </summary>
public class TestJobCertificate
{
    /// <summary>
    /// ID of the job the certificate belongs to. Primary key, cascades on job deletion.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The certificate subject. Default: empty string.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The certificate issuer. Default: empty string.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Start of the certificate's validity window. Default: null.
    /// </summary>
    public DateTime? NotBefore { get; set; }

    /// <summary>
    /// End of the certificate's validity window. Default: null.
    /// </summary>
    public DateTime? NotAfter { get; set; }

    /// <summary>
    /// Days until the certificate expires. Default: null.
    /// </summary>
    public double? DaysRemaining { get; set; }

    /// <summary>
    /// Subject alternative names, as a native text array. Default: empty array.
    /// </summary>
    public List<string> SubjectAlternativeNames { get; set; } = [];
}
