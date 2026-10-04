using Application.Abstraction.Data;
using Domain.Entities.FormReplyTemplates;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.FormReplyTemplates.SaveFormReplyTemplate;

internal sealed class SaveFormReplyTemplateCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<SaveFormReplyTemplateCommand, int>
{
    public async Task<Result<int>> Handle(SaveFormReplyTemplateCommand request, CancellationToken cancellationToken)
    {
        FormReplyTemplate template;

        if (request.Id is int id)
        {
            FormReplyTemplate? existing = await db.FormReplyTemplates
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (existing is null)
                return Result.Failure<int>(FormReplyTemplateErrors.NotFound);

            template = existing;
        }
        else
        {
            template = new FormReplyTemplate { Name = "", Subject = "", Body = "" };
            db.FormReplyTemplates.Add(template);
        }

        template.Name = request.Name.Trim();
        template.Subject = request.Subject.Trim();
        template.Body = request.Body;
        template.IsActive = request.IsActive;
        template.SortOrder = request.SortOrder;

        // SaveChanges runs in the pipeline behavior; the new row's identity is only
        // populated after it, so callers that need the id re-read rather than trust
        // this on create. Returning it anyway keeps update callers simple.
        return Result.Success(template.Id);
    }
}
