using Application.Abstraction.Data;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Redirects.UpdateRedirect;

internal sealed class UpdateRedirectCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<UpdateRedirectCommand>
{
    public async Task<Result> Handle(UpdateRedirectCommand request, CancellationToken cancellationToken)
    {
        Redirect? redirect = await db.Redirects
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (redirect is null)
            return Result.Failure(RedirectErrors.NotFound);

        string oldPath = RedirectPaths.NormalizeOld(request.OldPath);
        string? newPath = RedirectPaths.NormalizeNew(request.NewPath);

        Result guard = await RedirectRules.ValidateAsync(db, redirect.Id, oldPath, newPath, cancellationToken);
        if (guard.IsFailure)
            return guard;

        redirect.OldPath = oldPath;
        redirect.NewPath = newPath;
        redirect.IsTemporary = request.IsTemporary;

        // Editing by hand detaches the rule from the page that spawned it: the target
        // is now whatever the editor typed, so following the page would silently
        // overrule them on the next rename.
        if (redirect.SourcePageId is not null)
        {
            redirect.SourcePageId = null;
            redirect.Reason = RedirectReason.Manual;
        }

        return Result.Success();
    }
}
