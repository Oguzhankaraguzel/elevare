using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetBreadcrumbPreview;

/// <summary>
/// Mirrors the public site's BreadcrumbResolutionService: the page's ancestors up to
/// (not including) the homepage, only the live ones linked, the page itself last,
/// and the home crumb pointing at its own language's homepage. Keep them in step.
/// </summary>
internal sealed class GetBreadcrumbPreviewQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetBreadcrumbPreviewQuery, BreadcrumbPreviewResponse>
{
    private const string HomeSlug = "home";
    private const int MaxDepth = 20;

    public async Task<Result<BreadcrumbPreviewResponse>> Handle(GetBreadcrumbPreviewQuery request, CancellationToken cancellationToken)
    {
        List<BreadcrumbPreviewCrumb> reversed = [];
        string? homePath = null;
        int? languageId = null;
        bool isHome = false;
        int? cursor = request.PageId;

        for (int depth = 0; cursor is int id && depth < MaxDepth; depth++)
        {
            var page = await db.PageInfos.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new { p.Slug, p.FullSlug, p.ParentPageId, p.LanguageId, p.PageStatus, p.IsActive, p.SeoMeta.Title })
                .FirstOrDefaultAsync(cancellationToken);
            if (page is null)
                break;

            bool isSelf = depth == 0;
            languageId ??= page.LanguageId;
            if (page.ParentPageId is null && string.Equals(page.Slug, HomeSlug, StringComparison.OrdinalIgnoreCase))
            {
                homePath = PathOf(page.FullSlug);
                isHome = isSelf;
                if (!isSelf) break;
            }

            if (isSelf || page.PageStatus == PageStatus.Published && page.IsActive)
                reversed.Add(new BreadcrumbPreviewCrumb(string.IsNullOrWhiteSpace(page.Title) ? page.Slug : page.Title, PathOf(page.FullSlug)));

            cursor = page.ParentPageId;
        }

        if (homePath is null && languageId is int language)
        {
            string? homeSlug = await db.PageInfos.AsNoTracking()
                .Where(p => p.LanguageId == language && p.ParentPageId == null && p.Slug == HomeSlug)
                .Select(p => p.FullSlug)
                .FirstOrDefaultAsync(cancellationToken);
            homePath = homeSlug is null ? null : PathOf(homeSlug);
        }

        reversed.Reverse();
        return Result.Success(new BreadcrumbPreviewResponse(homePath ?? PathOf(""), isHome, reversed));
    }

    // A page's public address is "/" + its full slug.
#pragma warning disable S1075
    private static string PathOf(string fullSlug) => "/" + fullSlug;
#pragma warning restore S1075
}
