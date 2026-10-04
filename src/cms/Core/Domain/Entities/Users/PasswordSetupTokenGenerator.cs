using System.Security.Cryptography;

namespace Domain.Entities.Users;

/// <summary>
/// Generates the raw, one-time token that goes in a password-setup e-mail link, and
/// hashes it for storage/lookup in <see cref="PasswordSetupToken.TokenHash"/>. Kept
/// as a single shared helper so "how do we turn a raw token into what's stored" is
/// answered in exactly one place — issuing a link and validating one must agree on
/// it byte for byte.
/// </summary>
public static class PasswordSetupTokenGenerator
{
    /// <summary>A fresh, URL-safe, 256-bit random token.</summary>
    public static string CreateRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>
    /// SHA-256 of the raw token, hex-encoded. One-way on purpose: the value this
    /// method returns is the only form of the token ever written to the database.
    /// </summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
