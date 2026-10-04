using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteHealth.GetSiteHealth;

/// <summary>
/// Gathers the conditions behind the CMS header's warning indicator.
/// <para>
/// Every probe is best-effort and independent: one unavailable subsystem must not
/// hide what the others found, because the indicator's whole job is to be the one
/// place an operator can trust to say "something is off". A probe that cannot
/// answer is therefore reported AS an issue rather than swallowed.
/// </para>
/// <para>
/// Polled every 30 seconds per open circuit (see <c>SiteHealthIndicator.razor</c>),
/// and its own two dependencies each make an outbound HTTP call — so this request
/// implements <see cref="IBypassDbConcurrencyGuard"/> and reads through
/// <see cref="ICmsApplicationDbContextFactory"/> rather than the shared scoped
/// context: without both, a slow public-site probe would serialize behind, and
/// block, every other MediatR request the same circuit tries to make meanwhile.
/// </para>
/// </summary>
internal sealed class GetSiteHealthQueryHandler(
    ICmsApplicationDbContextFactory dbFactory,
    ICacheClearService cacheClearService,
    IPublicSiteProbe publicSiteProbe)
    : IQueryHandler<GetSiteHealthQuery, SiteHealthResponse>
{
    private const string MaintenanceKey = "Advanced.MaintenanceModeEnabled";
    private const string PublicSiteBaseUrlKey = "Advanced.PublicSiteBaseUrl";
    private const string SeoDefaultDescriptionKey = "Seo.DefaultMetaDescription";
    private const string CaptchaProviderKey = "Integrations.CaptchaProvider";
    private const string CaptchaSecretKey = "Captcha:SecretKey";

    /// <summary>
    /// Past this, the site is still up but a visitor is already waiting on it.
    /// </summary>
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(2);

    public async Task<Result<SiteHealthResponse>> Handle(
        GetSiteHealthQuery request,
        CancellationToken cancellationToken)
    {
        var issues = new List<SiteHealthIssue>();

        // ── Settings-derived signals (one round trip for both keys) ──
        try
        {
            await dbFactory.ExecuteAsync(async db =>
            {
                Dictionary<string, string?> settings = await db.SiteSettings
                    .Where(s => s.Key == MaintenanceKey || s.Key == PublicSiteBaseUrlKey || s.Key == SeoDefaultDescriptionKey || s.Key == CaptchaProviderKey)
                    .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

                if (settings.TryGetValue(MaintenanceKey, out string? maintenance)
                    && string.Equals(maintenance, "true", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new SiteHealthIssue(
                        SiteHealthCodes.MaintenanceMode,
                        SiteHealthSeverity.Warning,
                        Detail: null,
                        ActionPath: "/admin/settings"));
                }

                settings.TryGetValue(PublicSiteBaseUrlKey, out string? baseUrl);
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    issues.Add(new SiteHealthIssue(
                        SiteHealthCodes.PublicSiteBaseUrlMissing,
                        SiteHealthSeverity.Warning,
                        Detail: null,
                        ActionPath: "/admin/settings"));
                }

                settings.TryGetValue(SeoDefaultDescriptionKey, out string? seoDescription);
                if (string.IsNullOrWhiteSpace(seoDescription))
                {
                    issues.Add(new SiteHealthIssue(
                        SiteHealthCodes.SeoDefaultDescriptionMissing,
                        SiteHealthSeverity.Warning,
                        Detail: null,
                        ActionPath: "/admin/seo"));
                }

                if (settings.TryGetValue(CaptchaProviderKey, out string? captchaProvider)
                    && !string.IsNullOrWhiteSpace(captchaProvider)
                    && !string.Equals(captchaProvider, "none", StringComparison.OrdinalIgnoreCase))
                {
                    bool hasSecret = await db.IntegrationSecrets
                        .AnyAsync(s => s.Key == CaptchaSecretKey && !string.IsNullOrEmpty(s.Value), cancellationToken);

                    if (!hasSecret)
                    {
                        issues.Add(new SiteHealthIssue(
                            SiteHealthCodes.CaptchaSecretMissing,
                            SiteHealthSeverity.Error,
                            Detail: null,
                            ActionPath: "/admin/settings"));
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            issues.Add(new SiteHealthIssue(
                SiteHealthCodes.HealthCheckFailed,
                SiteHealthSeverity.Error,
                Detail: ex.Message,
                ActionPath: null));
        }

        // ── Cache backend ──
        // Fail-open by design (see ICacheService): a dead cache costs speed, not
        // correctness, so nothing else surfaces it. That is exactly why it belongs
        // here — otherwise the site quietly runs slow and nobody is told.
        Result<SiteCacheStatus> cache = await cacheClearService.GetStatusAsync(cancellationToken);
        if (cache.IsSuccess && !cache.Value.Healthy)
        {
            issues.Add(new SiteHealthIssue(
                SiteHealthCodes.CacheUnhealthy,
                SiteHealthSeverity.Error,
                Detail: cache.Value.Error,
                ActionPath: "/admin/cache"));
        }

        // A failed status call is NOT reported: GetStatusAsync already fails when
        // no public site URL is configured, which the settings probe above covers.
        // Reporting both would show the same misconfiguration twice.

        // ── Is the public site actually serving? ──
        // Deliberately last and deliberately separate: the checks above all read the
        // database, and the database is precisely what stays healthy while visitors
        // get nothing. Without this, a total outage looks like "all clear" here.
        Result<PublicSiteStatus> site = await publicSiteProbe.CheckAsync(cancellationToken);
        if (site.IsSuccess)
        {
            PublicSiteStatus status = site.Value;

            if (!status.Reachable)
            {
                issues.Add(new SiteHealthIssue(
                    SiteHealthCodes.PublicSiteDown,
                    SiteHealthSeverity.Error,
                    Detail: status.Error,
                    ActionPath: null));
            }
            else if (!status.Healthy)
            {
                issues.Add(new SiteHealthIssue(
                    SiteHealthCodes.PublicSiteErroring,
                    SiteHealthSeverity.Error,
                    Detail: $"HTTP {status.StatusCode}",
                    ActionPath: "/admin/logs"));
            }
            else if (status.ResponseTime > SlowThreshold)
            {
                issues.Add(new SiteHealthIssue(
                    SiteHealthCodes.PublicSiteSlow,
                    SiteHealthSeverity.Warning,
                    Detail: $"{status.ResponseTime.TotalMilliseconds:F0} ms",
                    ActionPath: "/admin/cache"));
            }
        }
        // A probe failure here means the base URL is unset, which the settings check
        // above already reports — same reasoning as the cache status call.

        return Result.Success(new SiteHealthResponse(issues));
    }
}
