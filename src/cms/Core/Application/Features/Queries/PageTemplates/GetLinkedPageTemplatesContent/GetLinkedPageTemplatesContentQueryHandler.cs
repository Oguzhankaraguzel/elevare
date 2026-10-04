using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.PageTemplates.GetLinkedPageTemplatesContent;

internal sealed class GetLinkedPageTemplatesContentQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetLinkedPageTemplatesContentQuery, Dictionary<int, LinkedPageTemplateContent>>
{
    public async Task<Result<Dictionary<int, LinkedPageTemplateContent>>> Handle(
        GetLinkedPageTemplatesContentQuery request,
        CancellationToken cancellationToken)
    {
        Dictionary<int, LinkedPageTemplateContent> map = await db.PageTemplates
            .AsNoTracking()
            .Where(t => t.IsLinked)
            .ToDictionaryAsync(
                t => t.Id,
                t => new LinkedPageTemplateContent(t.GjsHtml, t.GjsCss),
                cancellationToken);

        return Result.Success(map);
    }
}
