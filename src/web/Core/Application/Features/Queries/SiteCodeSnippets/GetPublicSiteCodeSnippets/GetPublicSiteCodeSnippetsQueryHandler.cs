using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteCodeSnippets.GetPublicSiteCodeSnippets;

internal sealed class GetPublicSiteCodeSnippetsQueryHandler(IPublicReadDbContext db, ICacheService cache)
    : IQueryHandler<GetPublicSiteCodeSnippetsQuery, Dictionary<int, List<PublicSiteCodeEntry>>>
{
    // Versioned: the cached shape changed from plain strings to entries with ids,
    // and a cache filled by the previous build must not be read back as the new one.
    private const string CacheKey = "sitecodesnippets:enabled:v2";

    // Same 30s window as the site settings this sits beside in the layout: every
    // page render reads both, and a tag added in the CMS should show up about as
    // fast as a setting change does.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<Result<Dictionary<int, List<PublicSiteCodeEntry>>>> Handle(
        GetPublicSiteCodeSnippetsQuery request, CancellationToken cancellationToken)
    {
        Dictionary<int, List<PublicSiteCodeEntry>>? cached =
            await cache.GetAsync<Dictionary<int, List<PublicSiteCodeEntry>>>(CacheKey, cancellationToken);

        if (cached is not null)
            return Result.Success(cached);

        // Ordered here rather than in the view: SortOrder is the whole mechanism by
        // which a consent banner is guaranteed to be parsed ahead of the trackers it
        // gates, and that guarantee should not depend on how a template loops.
        var rows = await db.SiteCodeSnippets
            .Where(s => s.IsEnabled && s.Content != null && s.Content != "")
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.Placement, s.Content })
            .ToListAsync(cancellationToken);

        var byPlacement = rows
            .GroupBy(r => r.Placement)
            .ToDictionary(g => g.Key, g => g.Select(r => new PublicSiteCodeEntry(r.Id, r.Content!)).ToList());

        await cache.SetAsync(CacheKey, byPlacement, CacheTtl, cancellationToken);
        return Result.Success(byPlacement);
    }
}
