using Application.Abstraction.Data;
using Domain.Entities.PageTemplates;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.PageTemplates.DeletePageTemplate;

internal sealed class DeletePageTemplateCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<DeletePageTemplateCommand>
{
    public async Task<Result> Handle(DeletePageTemplateCommand request, CancellationToken cancellationToken)
    {
        PageTemplate? template = await db.PageTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure(PageTemplateErrors.NotFound);

        // Soft delete (the audit interceptor turns Remove into IsDeleted = true).
        db.PageTemplates.Remove(template);

        return Result.Success();
    }
}
