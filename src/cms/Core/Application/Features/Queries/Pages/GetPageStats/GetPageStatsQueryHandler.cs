using Application.Abstraction.Data;
using Domain.Entities.Analytics;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPageStats;

/// <summary>
/// Per-page telemetry for the Pages list's stats popover — matched against
/// <c>PageViewHits</c>/<c>PageClickHits</c> by the page's CURRENT public path(s).
/// A slug rename loses the history recorded under the old path (the redirect
/// keeps visitors flowing, but old hits aren't retroactively relabeled); this is a
/// known, documented limitation rather than something worth a historical-path join.
/// </summary>
internal sealed class GetPageStatsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageStatsQuery, PageStatsResponse>
{
    /// <summary>Mirrors PageController.HomeSlug (Web) — a page with this slug renders at "/" (or "/{languageCode}") instead of its literal slug path.</summary>
    internal const string HomeSlug = "home";

    public async Task<Result<PageStatsResponse>> Handle(GetPageStatsQuery request, CancellationToken cancellationToken)
    {
        PageInfo? page = await db.PageInfos
            .AsNoTracking()
            .Include(p => p.Language)
            .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

        if (page is null)
            return Result.Failure<PageStatsResponse>(PageInfoErrors.NotFound);

        List<string> paths = BuildPathCandidates(page);

        IQueryable<PageViewHit> hits = db.PageViewHits.Where(h => paths.Contains(h.Path));

        int totalViews = await hits.CountAsync(cancellationToken);
        int uniqueVisitors = await hits.Select(h => h.VisitorId).Distinct().CountAsync(cancellationToken);
        double avgDuration = await hits
            .Where(h => h.DurationSeconds != null)
            .AverageAsync(h => (double?)h.DurationSeconds, cancellationToken) ?? 0;

        int totalClicks = await db.PageClickHits.CountAsync(h => paths.Contains(h.Path), cancellationToken);

        var response = new PageStatsResponse(
            page.SeoMeta.SeoScore,
            page.SeoMeta.SeoScoreUpdatedAt,
            totalViews,
            uniqueVisitors,
            avgDuration,
            totalClicks);

        return Result.Success(response);
    }

#pragma warning disable S1075 // Not a filesystem/config path — mirrors Web's URL routing convention.
    /// <summary>
    /// Every path a visitor could actually have hit for this page, per the Web
    /// routing rules in PageController: default-language pages serve at "/{slug}",
    /// other languages at "/{languageCode}/{slug}", and the reserved "home" slug
    /// serves at "/" ("/{languageCode}" for other languages) INSTEAD of "/home" —
    /// though "/…/home" also still resolves to the same page, so both are matched.
    /// </summary>
    /// <summary>Shared with <c>GetDashboardOverviewQueryHandler</c> for the home page's "last published page" stat.</summary>
    internal static List<string> BuildPathCandidates(PageInfo page)
    {
        string langPrefix = page.Language.IsDefault ? "" : "/" + page.Language.TwoLetterCode;
        string slugPath = langPrefix + "/" + page.FullSlug;

        if (page.Slug != HomeSlug)
            return [slugPath];

        string homePath = langPrefix.Length > 0 ? langPrefix : "/";
        return [homePath, slugPath];
    }
#pragma warning restore S1075
}
