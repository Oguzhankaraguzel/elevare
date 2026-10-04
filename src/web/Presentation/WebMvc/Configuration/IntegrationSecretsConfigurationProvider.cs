using Application.Abstraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedKernel.Concrete;

namespace WebMvc.Configuration;

/// <summary>
/// Web's counterpart to the CMS's own provider of the same name — see that one
/// for the full design rationale. The only difference: this side never reloads
/// on its own initiative, only when told to by <c>POST /api/secrets/reload</c>
/// (see <c>SecretsEndpoints</c>), since the CMS is the only writer of
/// <c>IntegrationSecrets</c>.
/// </summary>
internal sealed class IntegrationSecretsConfigurationProvider(
    Dictionary<string, string?> initialData,
    string connectionString,
    string? dataProtectionKeyPath,
    ILoggerFactory loggerFactory)
    : ConfigurationProvider, IIntegrationSecretsReloader
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("IntegrationSecretsConfiguration");

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
            return Result.Failure(IntegrationSecretsErrors.ReloadFailed(ex.Message));
        }
    }
}
