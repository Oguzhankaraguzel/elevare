using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetDeletedTranslation;

internal sealed class GetDeletedTranslationQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetDeletedTranslationQuery, DeletedTranslationResponse?>
{
    public async Task<Result<DeletedTranslationResponse?>> Handle(
        GetDeletedTranslationQuery request, CancellationToken cancellationToken)
    {
        PageInfo? source = await db.PageInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.SourcePageId, cancellationToken);

        if (source is null)
            return Result.Success<DeletedTranslationResponse?>(null);

        // Matched on the page group first — that is what actually ties translations
        // together. A page with no group yet has no siblings to have deleted.
        DeletedTranslationResponse? deleted = source.PageGroupId is null
            ? null
            : await db.PageInfos
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.IsDeleted
                            && p.PageGroupId == source.PageGroupId
                            && p.LanguageId == request.TargetLanguageId)
                // Newest first: after several delete/recreate rounds the most recent
                // tombstone is the one the editor actually means.
                .OrderByDescending(p => p.DeleteDate)
                .Select(p => new DeletedTranslationResponse(
                    p.Id,
                    p.SeoMeta.Title,
                    p.Slug,
                    p.DeleteDate,
                    p.DeleteUser == null ? null : (p.DeleteUser.FullName ?? p.DeleteUser.UserName)))
                .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(deleted);
    }
}
