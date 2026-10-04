using System.Security.Cryptography;
using Application.Abstraction.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Security;

internal sealed class DataProtectionSecretCrypto : IIntegrationSecretCrypto
{
    /// <summary>
    /// A dedicated purpose string isolates this protector from every other use of the
    /// same key ring (auth cookies, antiforgery) — two protectors created with
    /// different purposes can never decrypt each other's output, by design.
    /// </summary>
    private const string Purpose = "Elevare.IntegrationSecrets.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionSecretCrypto> _logger;

    public DataProtectionSecretCrypto(IDataProtectionProvider provider, ILogger<DataProtectionSecretCrypto> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string? Unprotect(string ciphertext)
    {
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt an integration secret — key ring may have changed.");
            return null;
        }
    }
}
