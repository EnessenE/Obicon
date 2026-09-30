using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// HTTPS test.
/// </summary>
public class HttpsTestRunner : HttpTestRunnerBase
{
    /// <inheritdoc />
    protected override string Scheme => "https://";

    /// <inheritdoc />
    public override TestType Type => TestType.Https;

    public HttpsTestRunner(ILogger<HttpsTestRunner> logger) : base(logger)
    {
    }
}
