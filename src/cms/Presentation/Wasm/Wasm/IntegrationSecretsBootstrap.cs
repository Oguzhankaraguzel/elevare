using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace Wasm;

/// <summary>
/// Reads <c>IntegrationSecrets</c> rows straight out of Postgres, very early in
/// startup — before <c>builder.Build()</c>, so before dependency injection (and
/// <c>ApplicationDbContext</c> with it) exists. Raw ADO.NET rather than EF for
/// exactly that reason: there is no container yet to resolve a <c>DbContext</c> from.
/// <para>
/// The returned pairs are layered into <see cref="IConfiguration"/> on top of
/// <c>appsettings.json</c> (see <c>Program.cs</c>), so every existing
/// <c>IOptions&lt;EmailOptions&gt;</c>/<c>IOptions&lt;ObjectStorageOptions&gt;</c>
/// consumer picks them up unchanged — this file is the only thing that knows the
/// database is now also a configuration source.
/// </para>
/// </summary>
internal static class IntegrationSecretsBootstrap
{
    /// <summary>
    /// Must match <c>DataProtectionSecretCrypto</c>'s purpose string exactly, or a
    /// value encrypted by the running app cannot be decrypted here — the two are
    /// compiled into different projects and cannot share the constant directly.
    /// </summary>
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
            command.CommandText = """SELECT "Key", "Value", "IsSecret" FROM "IntegrationSecrets" WHERE "IsDeleted" = false""";

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
                    provider ??= CreateProtector(dataProtectionKeyPath);
                    value = TryUnprotect(provider, value, key, logger);
                    if (value is null)
                        continue;
                }

                overrides[key] = value;
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or PostgresException or InvalidOperationException)
        {
            // Table doesn't exist yet (first boot, before migrations run) or the
            // database isn't reachable yet — either way, appsettings.json/env values
            // carry on working exactly as before. Migrations + seeding run moments
            // later in the same startup and create the table for next time.
            logger.LogDebug(ex, "Could not load integration secrets at startup — falling back to file/env configuration.");
        }

        return overrides;
    }

    private static IDataProtectionProvider CreateProtector(string? keyPath) =>
        string.IsNullOrWhiteSpace(keyPath)
            ? DataProtectionProvider.Create("Elevare.Cms")
            : DataProtectionProvider.Create(new DirectoryInfo(keyPath), b => b.SetApplicationName("Elevare.Cms"));

    private static string? TryUnprotect(IDataProtectionProvider provider, string ciphertext, string key, ILogger logger)
    {
        try
        {
            return provider.CreateProtector(ProtectorPurpose).Unprotect(ciphertext);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            logger.LogWarning(ex, "Failed to decrypt integration secret '{Key}' at startup — leaving file/env value in place.", key);
            return null;
        }
    }
}
