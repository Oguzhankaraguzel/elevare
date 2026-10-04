using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetAdjacentPreview;

/// <summary>
/// Mirrors the public site's AdjacentPageResolutionService: the live pages under the
/// same parent, in the same language, just before and after this one by publish
/// date (creation date until it has one), the id breaking a tie. Keep them in step.
/// </summary>
internal sealed class GetAdjacentPreviewQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetAdjacentPreviewQuery, AdjacentPreviewResponse>
{
    public async Task<Result<AdjacentPreviewResponse>> Handle(GetAdjacentPreviewQuery request, CancellationToken cancellationToken)
    {
        var current = await db.PageInfos.AsNoTracking()
            .Where(p => p.Id == request.PageId)
            .Select(p => new { p.Id, p.ParentPageId, p.LanguageId, Date = p.PublishedAt ?? p.CreateDate })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is not { ParentPageId: not null })
            return Result.Success(new AdjacentPreviewResponse(null, null));

        IQueryable<PageInfo> siblings = db.PageInfos.AsNoTracking().Where(p =>
            p.ParentPageId == current.ParentPageId && p.LanguageId == current.LanguageId && p.Id != current.Id
            && p.PageStatus == PageStatus.Published && p.IsActive);

        var previous = await siblings
            .Where(p => (p.PublishedAt ?? p.CreateDate) < current.Date
                || (p.PublishedAt ?? p.CreateDate) == current.Date && p.Id < current.Id)
            .OrderByDescending(p => p.PublishedAt ?? p.CreateDate).ThenByDescending(p => p.Id)
            .Select(p => new { p.SeoMeta.Title, p.Slug, p.FullSlug })
            .FirstOrDefaultAsync(cancellationToken);
        var next = await siblings
            .Where(p => (p.PublishedAt ?? p.CreateDate) > current.Date
                || (p.PublishedAt ?? p.CreateDate) == current.Date && p.Id > current.Id)
            .OrderBy(p => p.PublishedAt ?? p.CreateDate).ThenBy(p => p.Id)
            .Select(p => new { p.SeoMeta.Title, p.Slug, p.FullSlug })
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(new AdjacentPreviewResponse(
            previous is null ? null : Item(previous.Title, previous.Slug, previous.FullSlug),
            next is null ? null : Item(next.Title, next.Slug, next.FullSlug)));
    }

    private static AdjacentPreviewItem Item(string title, string slug, string fullSlug) =>
        new(string.IsNullOrWhiteSpace(title) ? slug : title, PathOf(fullSlug));

    // A page's public address is "/" + its full slug.
#pragma warning disable S1075
    private static string PathOf(string fullSlug) => "/" + fullSlug;
#pragma warning restore S1075
}
