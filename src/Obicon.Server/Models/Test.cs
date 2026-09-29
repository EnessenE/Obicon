using Obicon.Server.Models.Enums;
using Obicon.Shared.Models.Enums;

namespace Obicon.Server.Models;

public class Test
{
    /// <summary>
    /// Unique identifier for the test. Generated automatically on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the test. Default: empty string.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of test to execute. See <see cref="TestType"/> for available types.
    /// </summary>
    public TestType Type { get; set; }

    /// <summary>
    /// Target of the test: URL for HTTP(S), host:port for TCP, hostname or IP for the rest. Default: empty string.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// List of node IDs that should execute this test. Default: empty list.
    /// </summary>
    public List<Guid> NodeIds { get; set; } = new();

    /// <summary>
    /// List of pool IDs this test targets; all member nodes of these pools execute it too. Default: empty list.
    /// </summary>
    public List<Guid> PoolIds { get; set; } = new();

    /// <summary>
    /// How often the test should be executed. See <see cref="TestFrequency"/> for available frequencies.
    /// </summary>
    public TestFrequency Frequency { get; set; }

    /// <summary>
    /// Indicates if the test is currently active and should be scheduled. Default: true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// HTTP/HTTPS: accepted status codes, e.g. "200-399" or "200,301,302". Default: "200-399".
    /// </summary>
    public string ExpectedStatusCodes { get; set; } = "200-399";

    /// <summary>
    /// HTTPS: when set, the test fails if the TLS certificate expires within this many days.
    /// Null disables the expiry check. Default: null.
    /// </summary>
    public int? CheckCertificateExpiryDays { get; set; }

    /// <summary>
    /// DNS: when set, the test only succeeds if this address is among the resolved addresses.
    /// Null accepts any successfully resolved result. Default: null.
    /// </summary>
    public string? ExpectedDnsResult { get; set; }

    /// <summary>
    /// IP version the test should use. Default: Any.
    /// </summary>
    public IpVersion IpVersion { get; set; } = IpVersion.Any;

    /// <summary>
    /// Timestamp when the scheduler last enqueued this test. Null if never scheduled.
    /// </summary>
    public DateTime? LastScheduledAt { get; set; }

    /// <summary>
    /// Timestamp when the test was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last update to the test. Null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
