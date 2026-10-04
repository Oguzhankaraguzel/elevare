using Application.Abstraction.Services;
using Domain.Entities.IntegrationSecrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedKernel.Concrete;

namespace Wasm.Configuration;

/// <summary>
/// The live-reloadable counterpart to a one-shot <c>AddInMemoryCollection</c>:
/// seeded once with the values <c>Program.cs</c> already read from
/// <c>IntegrationSecrets</c> before <c>builder.Build()</c>, then re-readable on
/// demand via <see cref="ReloadAsync"/> — which calls <see cref="OnReload"/>, the
/// same mechanism <c>JsonConfigurationProvider</c> uses when its file changes, so
/// every <c>IOptionsMonitor&lt;T&gt;</c> bound against this configuration picks
/// the new values up without a restart.
/// <para>
/// Registered directly as a DI singleton instance from <c>Program.cs</c> (not
/// resolved from the container — it has to exist before the container does), so
/// the same object serves two roles: an <see cref="IConfigurationProvider"/> the
/// framework already knows about, and the <see cref="IIntegrationSecretsReloader"/>
/// the update/clear command handlers call after a save.
/// </para>
/// </summary>
internal sealed class IntegrationSecretsConfigurationProvider(
    Dictionary<string, string?> initialData,
    string connectionString,
    string? dataProtectionKeyPath,
    ILoggerFactory loggerFactory)
    : ConfigurationProvider, IIntegrationSecretsReloader
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("IntegrationSecretsConfiguration");

    /// <summary>
    /// Called synchronously by the framework during <c>builder.Build()</c>. No I/O
    /// here — the real (async) database read already happened in <c>Program.cs</c>
    /// before this provider was constructed; this just seeds <see cref="ConfigurationProvider.Data"/>
    /// with that result, exactly like the <c>AddInMemoryCollection(overrides)</c> it replaces.
    /// </summary>
    public override void Load() => Data = initialData!;

    public async Task<Result> ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string?> overrides = await IntegrationSecretsBootstrap.LoadAsync(
                connectionString, dataProtectionKeyPath, _logger);

            Data = overrides!;
            OnReload();

            return Result.Success();
        }
        catch (Exception ex) when (ex is NpgsqlException or PostgresException or InvalidOperationException)
        {
            _logger.LogError(ex, "Reloading integration secrets failed — configuration stays at its previous value.");
            return Result.Failure(IntegrationSecretErrors.ReloadFailed(ex.Message));
        }
    }
}
