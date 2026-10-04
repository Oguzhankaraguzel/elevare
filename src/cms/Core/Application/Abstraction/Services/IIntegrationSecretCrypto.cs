namespace Application.Abstraction.Services;

/// <summary>
/// Encrypts/decrypts <see cref="Domain.Entities.IntegrationSecrets.IntegrationSecret"/>
/// values before they touch the database. Backed by ASP.NET Data Protection, keyed to
/// the same <c>/app/keys</c> ring the app already persists sessions and antiforgery
/// tokens with — losing that ring (a fresh volume, a key rotated out) means these
/// values stop decrypting too, the same way an existing session would stop validating.
/// </summary>
public interface IIntegrationSecretCrypto
{
    string Protect(string plaintext);

    /// <summary>
    /// Null on a value that fails to decrypt (wrong/rotated key ring) rather than
    /// throwing — a handler can then treat the secret as "not set" instead of a 500.
    /// </summary>
    string? Unprotect(string ciphertext);
}
