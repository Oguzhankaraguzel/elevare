using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Languages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Languages.GetLanguages;

internal sealed record GetLanguagesQueryHandler(ICmsApplicationDbContext Db)
    : IQueryHandler<GetLanguagesQuery, PagedResult<LanguageResponse>>
{
    public async Task<Result<PagedResult<LanguageResponse>>> Handle(
        GetLanguagesQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<Language> query = Db.Languages
            .AsNoTracking()
            .Where(l => !l.IsDeleted);

        if (request.IsActive.HasValue)
        {
            query = query.Where(l => l.IsActive == request.IsActive.Value);
        }

        List<LanguageResponse> items = await query
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new LanguageResponse(
                l.Id,
                l.NameInNative,
                l.NameInEnglish,
                l.TwoLetterCode,
                l.FlagIconFileId,
                l.FlagIconFiles != null ? l.FlagIconFiles.FilePath : null,
                l.FlagIconFiles != null ? l.FlagIconFiles.AltText : null,
                l.IsDefault,
                l.IsRtl,
                l.IsPublished,
                l.DisplayOrder,
                l.IsActive))
            .ToListAsync(cancellationToken);

        return PagedResult<LanguageResponse>.Create(items, items.Count, 1, items.Count);
    }
}
