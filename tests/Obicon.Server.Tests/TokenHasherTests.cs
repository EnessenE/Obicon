using Obicon.Server.Services;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Unit tests for the token hashing used for both node auth tokens and enroll tokens.
/// </summary>
public class TokenHasherTests
{
    [Fact]
    public void Hash_IsDeterministic_AndHexEncoded()
    {
        var first = TokenHasher.Hash("my-plain-token");
        var second = TokenHasher.Hash("my-plain-token");

        Assert.Equal(first, second);
        Assert.Matches("^[0-9A-F]{64}$", first);
    }

    [Fact]
    public void Hash_DiffersPerInput()
    {
        Assert.NotEqual(TokenHasher.Hash("token-a"), TokenHasher.Hash("token-b"));
    }

    [Fact]
    public void Hash_NeverContainsThePlainValue()
    {
        var plain = "super-secret-plain-token";

        Assert.DoesNotContain(plain, TokenHasher.Hash(plain));
    }
}
