using Xunit;

namespace Obicon.Integration.Tests;

/// <summary>
/// xUnit collection sharing one Dockerized Obicon stack between all integration
/// tests, so the expensive image builds and container startup happen once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ObiconStackCollectionDefinition : ICollectionFixture<ObiconStackFixture>
{
    /// <summary>
    /// Name of the collection tests join to share the stack fixture.
    /// </summary>
    public const string Name = "Obicon stack";
}
