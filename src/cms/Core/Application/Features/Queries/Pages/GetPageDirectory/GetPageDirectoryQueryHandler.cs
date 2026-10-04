using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPageDirectory;

internal sealed class GetPageDirectoryQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageDirectoryQuery, List<PageDirectoryEntryResponse>>
{
    public async Task<Result<List<PageDirectoryEntryResponse>>> Handle(
        GetPageDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        List<PageDirectoryEntryResponse> pages = await db.PageInfos
            .AsNoTracking()
            .Where(p => request.LanguageId == null || p.LanguageId == request.LanguageId)
            .OrderBy(p => p.FullSlug)
            .Select(p => new PageDirectoryEntryResponse(p.Id, p.SeoMeta.Title, p.FullSlug))
            .ToListAsync(cancellationToken);

        return Result.Success(pages);
    }
}
