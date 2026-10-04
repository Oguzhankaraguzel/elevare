using AngleSharp.Dom;
using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Content;

namespace Application.Features.Queries.Pages.GetListingPreview;

/// <summary>
/// Mirrors the public site's PageListingResolutionService: same source rules, same
/// order, same card fallbacks (PageContentFacts, HumanDateFormatter). When the two
/// disagree, the editor promises a list the site does not show — keep them in step.
/// </summary>
internal sealed class GetListingPreviewQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetListingPreviewQuery, ListingPreviewResponse>
{
    private const int MaxTake = 100;
    private const int MaxHidden = 20;
    private const int SummaryLength = 200;
    private static readonly string[] SystemSlugs = ["home", "404", "500", "maintenance"];

    public async Task<Result<ListingPreviewResponse>> Handle(GetListingPreviewQuery request, CancellationToken cancellationToken)
    {
        var current = request.PageId is { } pageId
            ? await db.PageInfos.AsNoTracking()
                .Where(p => p.Id == pageId)
                .Select(p => new { p.Id, p.LanguageId, p.ParentPageId, LanguageCode = p.Language.TwoLetterCode })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        IQueryable<PageInfo>? scope = Scope(request, current?.Id, current?.LanguageId, current?.ParentPageId);
        if (scope is null)
            return Result.Success(new ListingPreviewResponse(0, [], []));

        if (request.RelatedTags && current is not null)
        {
            List<int> ownTags = await db.PageInfoTags
                .Where(pt => pt.PageInfoId == current.Id)
                .Select(pt => pt.TagId)
                .ToListAsync(cancellationToken);
            scope = scope.Where(p => p.Tags!.Any(t => ownTags.Contains(t.Id)));
        }

        if (!string.IsNullOrWhiteSpace(request.TagSlug))
        {
            string slug = request.TagSlug.Trim();
            scope = scope.Where(p => p.Tags!.Any(t => t.Slug == slug));
        }

        IQueryable<PageInfo> live = scope.Where(p => p.PageStatus == PageStatus.Published && p.IsActive);
        int publishedCount = await live.CountAsync(cancellationToken);

        List<PageInfo> pages = await Sort(live, request.Sort)
            .Include(p => p.Content)
            .Include(p => p.Tags)
            .AsNoTracking()
            .Take(Math.Clamp(request.Take, 1, MaxTake))
            .ToListAsync(cancellationToken);

        List<ListingPreviewItem> items = [];
        foreach (PageInfo page in pages)
            items.Add(await CardAsync(page, current?.LanguageCode, cancellationToken));

        var hidden = await scope
            .Where(p => !(p.PageStatus == PageStatus.Published && p.IsActive))
            .OrderByDescending(p => p.UpdateDate ?? p.CreateDate)
            .Take(MaxHidden)
            .Select(p => new { p.Id, p.SeoMeta.Title, p.Slug, p.PageStatus, p.PendingStatus, p.IsActive })
            .ToListAsync(cancellationToken);

        return Result.Success(new ListingPreviewResponse(
            publishedCount,
            items,
            [.. hidden.Select(h => new ListingPreviewHidden(
                h.Id,
                string.IsNullOrWhiteSpace(h.Title) ? h.Slug : h.Title,
                Reason(h.PageStatus, h.PendingStatus, h.IsActive)))]));
    }

    // The pages a listing draws from, before "is it live": the public site's
    // BuildBaseQuery, minus the status filter so the editor can name the ones left out.
    private IQueryable<PageInfo>? Scope(GetListingPreviewQuery request, int? currentId, int? languageId, int? parentId)
    {
        IQueryable<PageInfo> pages = db.PageInfos.AsNoTracking();
        switch (request.Source)
        {
            case "siblings":
                if (currentId is null || languageId is null) return null;
                return pages.Where(p => p.ParentPageId == parentId
                    && p.LanguageId == languageId
                    && p.Id != currentId
                    && !(p.ParentPageId == null && SystemSlugs.Contains(p.Slug)));
            case "specific":
                return request.SourcePageId is { } parent ? pages.Where(p => p.ParentPageId == parent) : null;
            case "all":
                if (currentId is null || languageId is null) return null;
                return pages.Where(p => p.LanguageId == languageId
                    && p.Id != currentId
                    && !(p.ParentPageId == null && SystemSlugs.Contains(p.Slug)));
            default:
                return currentId is { } self ? pages.Where(p => p.ParentPageId == self) : null;
        }
    }

    private static IQueryable<PageInfo> Sort(IQueryable<PageInfo> pages, string sort) => sort switch
    {
        "oldest" => pages.OrderBy(p => p.PublishedAt ?? p.CreateDate).ThenBy(p => p.Id),
        "title-asc" => pages.OrderBy(p => p.SeoMeta.Title).ThenBy(p => p.Id),
        "title-desc" => pages.OrderByDescending(p => p.SeoMeta.Title).ThenByDescending(p => p.Id),
        _ => pages.OrderByDescending(p => p.PublishedAt ?? p.CreateDate).ThenByDescending(p => p.Id),
    };

    private static async Task<ListingPreviewItem> CardAsync(PageInfo page, string? languageCode, CancellationToken cancellationToken)
    {
        string? summary = NullIfBlank(page.SeoMeta.MetaDescription);
        string? image = NullIfBlank(page.SeoMeta.OgImage);
        if (summary is null || image is null)
        {
            using IDocument? content = await PageContentFacts.ParseAsync(page.Content?.GjsHtml, cancellationToken);
            summary ??= PageContentFacts.OpeningText(content, SummaryLength);
            image ??= PageContentFacts.FirstImage(content);
        }

        List<string> tags = [.. (page.Tags ?? []).Where(t => !t.IsDeleted).Select(t => t.Name).Order(StringComparer.CurrentCulture)];
        // The page's public address, as the site builds it: "/" + FullSlug.
#pragma warning disable S1075
        string path = "/" + page.FullSlug;
#pragma warning restore S1075
        return new ListingPreviewItem(
            page.Id,
            string.IsNullOrWhiteSpace(page.SeoMeta.Title) ? page.Slug : page.SeoMeta.Title,
            path,
            summary,
            image,
            HumanDateFormatter.Format(page.PublishedAt ?? page.CreateDate, languageCode),
            tags.Count == 0 ? null : string.Join(", ", tags));
    }

    private static string Reason(PageStatus status, PageStatus? pending, bool isActive)
    {
        if (pending == PageStatus.Published) return "pending";
        if (status == PageStatus.Archived) return "archived";
        if (status == PageStatus.Published && !isActive) return "inactive";
        return "draft";
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
