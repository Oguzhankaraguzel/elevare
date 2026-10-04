using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.FormReplyTemplates.GetFormReplyTemplates;

internal sealed class GetFormReplyTemplatesQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetFormReplyTemplatesQuery, List<FormReplyTemplateResponse>>
{
    public async Task<Result<List<FormReplyTemplateResponse>>> Handle(
        GetFormReplyTemplatesQuery request, CancellationToken cancellationToken)
    {
        List<FormReplyTemplateResponse> templates = await db.FormReplyTemplates
            .Where(t => !request.ActiveOnly || t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .Select(t => new FormReplyTemplateResponse(t.Id, t.Name, t.Subject, t.Body, t.IsActive, t.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success(templates);
    }
}
