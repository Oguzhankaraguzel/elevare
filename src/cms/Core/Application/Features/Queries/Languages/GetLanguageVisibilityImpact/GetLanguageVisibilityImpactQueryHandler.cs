using Application.Abstraction.Data;
using Application.Features.Commands.Languages;
using Domain.Entities.Languages;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Languages.GetLanguageVisibilityImpact;

internal sealed class GetLanguageVisibilityImpactQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetLanguageVisibilityImpactQuery, LanguageVisibilityImpact>
{
    public async Task<Result<LanguageVisibilityImpact>> Handle(
        GetLanguageVisibilityImpactQuery request, CancellationToken cancellationToken)
    {
        string? code = await db.Languages
            .Where(l => l.Id == request.LanguageId)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (code is null)
            return Result.Failure<LanguageVisibilityImpact>(LanguageErrors.NotFound);

        // Counted the way visitors would: the error and maintenance pages are not
        // pages anyone opens on purpose.
        int pages = await db.PageInfos.CountAsync(
            p => p.LanguageId == request.LanguageId && p.PageStatus == PageStatus.Published
                && !SystemPageSlugs.All.Contains(p.Slug),
            cancellationToken);
        int redirects = await LanguageVisibilityRedirects.For(db, code).CountAsync(cancellationToken);

        return Result.Success(new LanguageVisibilityImpact(pages, redirects));
    }
}
