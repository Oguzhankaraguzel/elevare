using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteSettings.GetSiteIdentity;

internal sealed class GetSiteIdentityQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetSiteIdentityQuery, SiteIdentityResponse>
{
    private const string SiteNameKey = "General.SiteName";
    private const string PublicSiteBaseUrlKey = "Advanced.PublicSiteBaseUrl";
    private const string FaviconUrlKey = "Appearance.FaviconUrl";

    public async Task<Result<SiteIdentityResponse>> Handle(GetSiteIdentityQuery request, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> settings = await db.SiteSettings
            .AsNoTracking()
            .Where(s => !s.IsDeleted
                && (s.Key == SiteNameKey || s.Key == PublicSiteBaseUrlKey || s.Key == FaviconUrlKey))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string? siteUrl = null;
        string? host = null;
        if (Uri.TryCreate(Value(settings, PublicSiteBaseUrlKey), UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            siteUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        return Result.Success(new SiteIdentityResponse(
            Value(settings, SiteNameKey), siteUrl, host, Value(settings, FaviconUrlKey)));
    }

    private static string? Value(Dictionary<string, string?> settings, string key) =>
        settings.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
