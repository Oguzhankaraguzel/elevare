using Application.Abstraction.Data;
using Domain.Entities.PageTemplates;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.PageTemplates.GetPageTemplates;

internal sealed class GetPageTemplatesQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageTemplatesQuery, List<PageTemplateListItemResponse>>
{
    public async Task<Result<List<PageTemplateListItemResponse>>> Handle(
        GetPageTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<PageTemplate> query = db.PageTemplates.AsNoTracking();

        if (request.Type is PageTemplateType type)
            query = query.Where(t => t.Type == type);

        if (request.LanguageId is int languageId)
            query = query.Where(t => t.LanguageId == languageId);

        List<PageTemplateListItemResponse> items = await query
            .OrderByDescending(t => t.UpdateDate ?? t.CreateDate)
            .Select(t => new PageTemplateListItemResponse(
                t.Id, t.Name, t.Type, t.IsLinked, t.CreateDate, t.UpdateDate,
                t.LanguageId, t.Language != null ? t.Language.NameInNative : null))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
