using System.Security.Cryptography;
using System.Text;

namespace Obicon.Server.Services;

/// <summary>
/// Hashes plain tokens for storage. Only hashes are stored; the plain value is
/// returned to the caller exactly once, at creation.
/// </summary>
public static class TokenHasher
{
    /// <summary>
    /// Returns the hex-encoded SHA-256 hash of a plain token.
    /// </summary>
    public static string Hash(string plainToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken)));
    }
}
