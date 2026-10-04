namespace Application.Features.Queries.SiteHealth.GetSiteHealth;

/// <summary>
/// Stable identifiers for the conditions <c>GetSiteHealthQueryHandler</c> reports.
/// The header component maps each to a resx title/explanation, so both sides must
/// agree — hence a shared catalogue rather than loose strings at either end.
/// </summary>
public static class SiteHealthCodes
{
    /// <summary>The public site is closed to visitors.</summary>
    public const string MaintenanceMode = "MaintenanceMode";

    /// <summary>A cache backend is configured but not answering.</summary>
    public const string CacheUnhealthy = "CacheUnhealthy";

    /// <summary>
    /// <c>Advanced.PublicSiteBaseUrl</c> is empty, which leaves preview links and
    /// cache management with no site to talk to.
    /// </summary>
    public const string PublicSiteBaseUrlMissing = "PublicSiteBaseUrlMissing";

    /// <summary>
    /// The public site did not answer at all. The single most important thing this
    /// indicator can report: everything else here reads the database, which keeps
    /// working perfectly while visitors see nothing.
    /// </summary>
    public const string PublicSiteDown = "PublicSiteDown";

    /// <summary>The public site answered, but with an error status.</summary>
    public const string PublicSiteErroring = "PublicSiteErroring";

    /// <summary>The public site is serving, but slowly enough to be worth knowing.</summary>
    public const string PublicSiteSlow = "PublicSiteSlow";

    /// <summary>The health check itself could not complete.</summary>
    public const string HealthCheckFailed = "HealthCheckFailed";

    /// <summary>
    /// No site-wide fallback meta description is set — any page whose own SEO panel
    /// is left blank ships with no description at all, not a generic one.
    /// </summary>
    public const string SeoDefaultDescriptionMissing = "SeoDefaultDescriptionMissing";

    /// <summary>
    /// A captcha provider is chosen in Site Settings but no secret key is configured,
    /// which causes every public form submission to fail closed.
    /// </summary>
    public const string CaptchaSecretMissing = "CaptchaSecretMissing";
}
