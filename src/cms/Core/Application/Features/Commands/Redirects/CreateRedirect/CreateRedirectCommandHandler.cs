using Application.Abstraction.Data;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Redirects.CreateRedirect;

internal sealed class CreateRedirectCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<CreateRedirectCommand, int>
{
    public async Task<Result<int>> Handle(CreateRedirectCommand request, CancellationToken cancellationToken)
    {
        string oldPath = RedirectPaths.NormalizeOld(request.OldPath);
        string? newPath = RedirectPaths.NormalizeNew(request.NewPath);

        Result guard = await RedirectRules.ValidateAsync(db, null, oldPath, newPath, cancellationToken);
        if (guard.IsFailure)
            return Result.Failure<int>(guard.Error);

        var redirect = new Redirect
        {
            OldPath = oldPath,
            NewPath = newPath,
            // Hand-written rules are never bound to a page: the whole reason someone
            // types one is that no page rename produced it.
            SourcePageId = null,
            Reason = RedirectReason.Manual,
            IsTemporary = request.IsTemporary,
        };

        db.Redirects.Add(redirect);

        // Saved here rather than left to the pipeline so the caller gets the new id.
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(redirect.Id);
    }
}
