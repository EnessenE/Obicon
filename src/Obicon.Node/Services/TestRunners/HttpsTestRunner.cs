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
}
