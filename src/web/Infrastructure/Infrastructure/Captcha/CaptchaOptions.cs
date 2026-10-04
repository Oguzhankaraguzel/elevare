namespace Infrastructure.Captcha;

/// <summary>
/// The Captcha secret key, bound from <c>appsettings.json → Captcha:SecretKey</c> —
/// or, if set, from the encrypted <c>IntegrationSecrets</c> table (the CMS's "Sırlar"
/// screen), which is layered on top of the file at startup; see
/// <c>IntegrationSecretsBootstrap</c>. A save from "Sırlar" reaches here live (see
/// <c>IIntegrationSecretsReloader</c>, <c>SecretsEndpoints</c>) — read via
/// <c>IOptionsMonitor&lt;CaptchaOptions&gt;</c>, never a plain <c>IOptions&lt;T&gt;</c>,
/// wherever this is consumed. Pairs with the public site key and provider selector,
/// which live in plain Site Settings (<c>Integrations.CaptchaSiteKey</c>/
/// <c>CaptchaProvider</c>) since those are safe to expose.
/// </summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    public string SecretKey { get; init; } = "";
}
