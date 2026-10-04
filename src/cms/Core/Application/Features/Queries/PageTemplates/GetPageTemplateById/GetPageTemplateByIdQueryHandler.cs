using Application.Features.Commands.Pages;
using Application.Abstraction.Data;
using Domain.Entities.PageTemplates;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.PageTemplates.GetPageTemplateById;

internal sealed class GetPageTemplateByIdQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageTemplateByIdQuery, PageTemplateEditorResponse>
{
    public async Task<Result<PageTemplateEditorResponse>> Handle(
        GetPageTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        PageTemplate? template = await db.PageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure<PageTemplateEditorResponse>(PageTemplateErrors.NotFound);

        var response = new PageTemplateEditorResponse(
            template.Id, template.Name, template.Type, template.IsLinked,
            template.GjsHtml, template.GjsCss, template.GjsData, template.LanguageId,
            template.ContentChangedAt ?? template.UpdateDate ?? template.CreateDate,
            PageFingerprint.Of(template));

        return Result.Success(response);
    }
}
