using System.ComponentModel.DataAnnotations;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models.Requests;

/// <summary>
/// Runs a single test immediately on one node without creating a test first.
/// </summary>
public class RunTestOnceRequest
{
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
    /// IDs of the nodes to execute the test on. At least one node ID or pool ID is required.
    /// Jobs only go to connected nodes among the selection.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// IDs of pools to run the test on: the top 3 connected pool members (least busy first)
    /// are selected in addition to the explicit node IDs. Default: empty list.
    /// </summary>
    public List<Guid> PoolIds { get; set; } = new();

    /// <summary>
    /// Maximum execution time for this run in seconds, between 1 and 60. Default: 60.
    /// </summary>
    [Range(1, 60)]
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    [RegularExpression(@"^\d{3}(-\d{3})?(,\d{3}(-\d{3})?)*$", ErrorMessage = "Use status codes like 200-399 or 200,301,302")]
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days. Null disables the check.
    /// </summary>
    [Range(0, 3650)]
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses. Null accepts any result.
    /// </summary>
    public string? ExpectedDnsResult { get; set; }

    /// <summary>
    /// IP version the test should use. Default: Any.
    /// </summary>
    [EnumDataType(typeof(IpVersion))]
    public IpVersion IpVersion { get; set; } = IpVersion.Any;

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
