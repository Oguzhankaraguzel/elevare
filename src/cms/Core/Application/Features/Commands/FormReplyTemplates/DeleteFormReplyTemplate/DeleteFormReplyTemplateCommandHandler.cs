using Application.Abstraction.Data;
using Domain.Entities.FormReplyTemplates;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.FormReplyTemplates.DeleteFormReplyTemplate;

internal sealed class DeleteFormReplyTemplateCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<DeleteFormReplyTemplateCommand>
{
    public async Task<Result> Handle(DeleteFormReplyTemplateCommand request, CancellationToken cancellationToken)
    {
        FormReplyTemplate? template = await db.FormReplyTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure(FormReplyTemplateErrors.NotFound);

        // Soft delete (the audit interceptor turns Remove into IsDeleted = true).
        // Replies already sent keep their own copy of the text, so removing the
        // template never rewrites history.
        db.FormReplyTemplates.Remove(template);

        return Result.Success();
    }
}
