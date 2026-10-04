using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteSettings.GetPublicSiteSettings;

internal sealed class GetPublicSiteSettingsQueryHandler(IPublicReadDbContext db, ICacheService cache)
    : IQueryHandler<GetPublicSiteSettingsQuery, Dictionary<string, string?>>
{
    private const string CacheKey = "sitesettings:all";

    // Every page render reads this (see _Layout.cshtml), so caching it is one of the
    // highest-value spots for this mechanism. Kept short, like the page query's:
    // /api/cache/clear drops it on every CMS change anyway.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<Result<Dictionary<string, string?>>> Handle(
        GetPublicSiteSettingsQuery request,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string?>? cached = await cache.GetAsync<Dictionary<string, string?>>(CacheKey, cancellationToken);
        if (cached is not null)
            return Result.Success(cached);

        Dictionary<string, string?> map = await db.SiteSettings
            .Where(s => !s.IsDeleted)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        await cache.SetAsync(CacheKey, map, CacheTtl, cancellationToken);
        return Result.Success(map);
    }
}
