using System.ComponentModel.DataAnnotations;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Requests;

public class CreateTestRequest
{
    /// <summary>
    /// Human-readable name of the test. Required, at least 1 character.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types. Required.
    /// </summary>
    [EnumDataType(typeof(TestType))]
    public TestType Type { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Required.
    /// </summary>
    [Required(AllowEmptyStrings = false), MinLength(1)]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// List of node IDs that should execute this test. At least one node or pool is required. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// List of pool IDs this test targets; all member nodes of these pools execute it too. Default: empty list.
    /// </summary>
    public List<Guid> PoolIds { get; set; } = new();

    /// <summary>
    /// How often the test should be executed, in seconds. Must be one of the FrequencyPresetsSeconds server setting values.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Frequency { get; set; }

    /// <summary>
    /// Indicates if the test should be active immediately. Default: true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    [RegularExpression(@"^\d{3}(-\d{3})?(,\d{3}(-\d{3})?)*$", ErrorMessage = "Use status codes like 200-399 or 200,301,302")]
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days.
    /// Null disables the expiry check. Default: null.
    /// </summary>
    [Range(0, 3650)]
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses.
    /// Null accepts any successfully resolved result. Default: null.
    /// </summary>
    public string? ExpectedDnsResult { get; set; }

    /// <summary>
    /// IP version the test should use. Default: Any.
    /// </summary>
    [EnumDataType(typeof(IpVersion))]
    public IpVersion IpVersion { get; set; } = IpVersion.Any;

    /// <summary>
    /// Maximum execution time per run in seconds, between 1 and the server's MaxTestTimeoutSeconds. Default: 60.
    /// </summary>
    [Range(1, 3600)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// HTTP/HTTPS: when set, the response body must match this regular expression
    /// or the run fails. Null disables the check. Default: null.
    /// </summary>
    [MaxLength(2000)]
    public string? ExpectedBodyPattern { get; set; }

    /// <summary>
    /// HTTP/HTTPS: custom headers sent with the request, e.g. authentication headers. Default: empty dictionary.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// HTTP/HTTPS: absolute http:// or https:// URL of a proxy the request goes through.
    /// Null connects directly. Default: null.
    /// </summary>
    [MaxLength(500)]
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// HTTP/HTTPS: when true, a unique query parameter is appended to the request URL
    /// so caches serve a fresh response. Default: false.
    /// </summary>
    public bool CacheBust { get; set; }
}
