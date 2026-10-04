using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace WebMvc;

/// <summary>
/// Reads the <c>Captcha:*</c> rows out of <c>IntegrationSecrets</c> at startup —
/// the only integration secret the public site itself consumes (CAPTCHA
/// verification; SMTP and S3/CDN credentials are CMS-admin-side concerns). See the
/// CMS's own copy of this file for the full rationale; the two are duplicated
/// rather than shared because the CMS and the public site are separately compiled
/// and deployed apps that happen to share one database, not one assembly.
/// </summary>
internal static class IntegrationSecretsBootstrap
{
    /// <summary>Must match the CMS's <c>DataProtectionSecretCrypto</c> purpose string exactly.</summary>
    private const string ProtectorPurpose = "Elevare.IntegrationSecrets.v1";

    public static async Task<Dictionary<string, string?>> LoadAsync(
        string connectionString, string? dataProtectionKeyPath, ILogger logger)
    {
        Dictionary<string, string?> overrides = [];

        try
        {
            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync();

            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = """SELECT "Key", "Value", "IsSecret" FROM "IntegrationSecrets" WHERE "IsDeleted" = false AND "Key" LIKE 'Captcha:%'""";

            IDataProtectionProvider? provider = null;

            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string key = reader.GetString(0);
                string? value = await reader.IsDBNullAsync(1) ? null : reader.GetString(1);
                bool isSecret = reader.GetBoolean(2);

                if (string.IsNullOrEmpty(value))
                    continue;

                if (isSecret)
                {
                    // Same key ring the CMS writes with — this app never encrypts
                    // anything itself, so it uses the same application name for
                    // key derivation but has no matching Protect() call of its own.
                    provider ??= string.IsNullOrWhiteSpace(dataProtectionKeyPath)
                        ? DataProtectionProvider.Create("Elevare.Cms")
                        : DataProtectionProvider.Create(new DirectoryInfo(dataProtectionKeyPath), b => b.SetApplicationName("Elevare.Cms"));

                    try
                    {
                        value = provider.CreateProtector(ProtectorPurpose).Unprotect(value);
                    }
                    catch (System.Security.Cryptography.CryptographicException ex)
                    {
                        logger.LogWarning(ex, "Failed to decrypt integration secret '{Key}' at startup — leaving file/env value in place.", key);
                        continue;
                    }
                }

                overrides[key] = value;
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or PostgresException or InvalidOperationException)
        {
            logger.LogDebug(ex, "Could not load integration secrets at startup — falling back to file/env configuration.");
        }

        return overrides;
    }
}
