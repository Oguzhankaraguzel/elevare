using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Re-reads <c>IntegrationSecrets</c> from the database into this process's own
/// <c>IConfiguration</c> and fires its reload token, so every
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> consumer
/// (<c>EmailOptions</c>, <c>CaptchaOptions</c>, <c>ObjectStorageOptions</c>) picks
/// the new values up on its very next access — no restart.
/// <para>
/// Implemented by the configuration provider itself (see each project's own
/// <c>IntegrationSecretsConfigurationProvider</c>, registered before
/// <c>WebApplicationBuilder.Build()</c> for the same "no DI container yet" reason
/// <c>IntegrationSecretsBootstrap</c> already runs there), and registered as a
/// singleton so the command that just changed a secret can call it directly —
/// the same shape as <c>IRolePermissionCache.RefreshAsync</c>.
/// </para>
/// </summary>
public interface IIntegrationSecretsReloader
{
    Task<Result> ReloadAsync(CancellationToken cancellationToken = default);
}
