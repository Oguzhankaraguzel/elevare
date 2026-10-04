using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.PageTemplates.GetPageTemplateVersions;

internal sealed class GetPageTemplateVersionsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageTemplateVersionsQuery, Dictionary<int, PageTemplateVersionInfo>>
{
    public async Task<Result<Dictionary<int, PageTemplateVersionInfo>>> Handle(
        GetPageTemplateVersionsQuery request, CancellationToken cancellationToken)
    {
        Dictionary<int, PageTemplateVersionInfo> map = await db.PageTemplates
            .AsNoTracking()
            .ToDictionaryAsync(
                t => t.Id,
                t => new PageTemplateVersionInfo(t.Name, t.ContentChangedAt ?? t.UpdateDate ?? t.CreateDate),
                cancellationToken);

        return Result.Success(map);
    }
}
