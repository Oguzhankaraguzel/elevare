using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Re-reads the <c>Captcha:*</c> rows of <c>IntegrationSecrets</c> into this
/// process's own <c>IConfiguration</c> and fires its reload token, so
/// <c>IOptionsMonitor&lt;CaptchaOptions&gt;</c> picks the new secret key up on its
/// very next verification — no restart. Unlike the CMS's copy of this interface,
/// nothing in the Web app calls <see cref="ReloadAsync"/> on its own initiative;
/// it only ever runs in response to the CMS's <c>POST /api/secrets/reload</c>
/// (see <c>SecretsEndpoints</c>), since the CMS is the only place a secret is
/// ever written.
/// </summary>
public interface IIntegrationSecretsReloader
{
    Task<Result> ReloadAsync(CancellationToken cancellationToken = default);
}
