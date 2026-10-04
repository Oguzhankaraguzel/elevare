using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPageTranslations;

internal sealed class GetPageTranslationsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageTranslationsQuery, PageTranslationsResponse>
{
    public async Task<Result<PageTranslationsResponse>> Handle(
        GetPageTranslationsQuery request,
        CancellationToken cancellationToken)
    {
        PageInfo? page = await db.PageInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

        if (page is null)
            return Result.Failure<PageTranslationsResponse>(PageInfoErrors.NotFound);

        // Queried as a flat sibling lookup (not PageGroup.Include(g => g.Pages))
        // because Including PageGroup->Pages from PageInfo forms a self-referencing
        // include cycle, which EF Core rejects on AsNoTracking queries.
        List<PageTranslationItem> translations = page.PageGroupId is null
            ? []
            : await db.PageInfos
                .AsNoTracking()
                .Include(p => p.Language)
                .Where(p => p.PageGroupId == page.PageGroupId && p.Id != page.Id)
                .Select(p => new PageTranslationItem(
                    p.Id,
                    p.LanguageId,
                    p.Language.TwoLetterCode,
                    p.Language.NameInNative,
                    p.FullSlug,
                    p.PageStatus))
                .ToListAsync(cancellationToken);

        return Result.Success(new PageTranslationsResponse(page.PageGroupId, translations));
    }
}
