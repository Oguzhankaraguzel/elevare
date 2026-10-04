using Application.Abstraction.Data;
using Domain.Entities.SiteCodeSnippets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteCodeSnippets.DeleteSiteCodeSnippet;

internal sealed class DeleteSiteCodeSnippetCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<DeleteSiteCodeSnippetCommand>
{
    public async Task<Result> Handle(DeleteSiteCodeSnippetCommand request, CancellationToken cancellationToken)
    {
        SiteCodeSnippet? snippet = await db.SiteCodeSnippets
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (snippet is null)
            return Result.Failure(SiteCodeSnippetErrors.NotFound);

        // Soft delete (ApplySoftDeletes in the SaveChanges pipeline) — a tag removed
        // by mistake is recoverable from the row rather than from the vendor.
        db.SiteCodeSnippets.Remove(snippet);

        return Result.Success();
    }
}
