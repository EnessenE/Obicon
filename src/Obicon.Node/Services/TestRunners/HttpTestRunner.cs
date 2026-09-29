using Obicon.Shared.Models.Enums;

namespace Obicon.Node.Services.TestRunners;

/// <summary>
/// Plain HTTP test.
/// </summary>
public class HttpTestRunner : HttpTestRunnerBase
{
    /// <inheritdoc />
    protected override string Scheme => "http://";

    /// <inheritdoc />
    public override TestType Type => TestType.Http;
}
