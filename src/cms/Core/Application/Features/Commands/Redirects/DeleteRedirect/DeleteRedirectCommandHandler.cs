using Application.Abstraction.Data;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Redirects.DeleteRedirect;

internal sealed class DeleteRedirectCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<DeleteRedirectCommand>
{
    public async Task<Result> Handle(DeleteRedirectCommand request, CancellationToken cancellationToken)
    {
        Redirect? redirect = await db.Redirects
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (redirect is null)
            return Result.Failure(RedirectErrors.NotFound);

        // Soft delete via the audit interceptor. The filtered unique index on OldPath
        // ignores deleted rows, so the same path can be redirected again later.
        db.Redirects.Remove(redirect);

        return Result.Success();
    }
}
