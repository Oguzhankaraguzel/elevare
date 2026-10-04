using Application.Abstraction.Data;
using Domain.Entities.Tags;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Tags.DeleteTag;

internal sealed class DeleteTagCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<DeleteTagCommand>
{
    public async Task<Result> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        Tag? tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag is null)
            return Result.Failure(TagErrors.NotFound);

        // Soft delete, so the join rows in PageInfoTags are left intact. The tag
        // simply stops appearing — on pages, in the picker and in queries — and a
        // restore from the Trash brings it back on the same pages it was on before.
        // A hard delete would cascade those rows away and make the restore a lie.
        db.Tags.Remove(tag);

        return Result.Success();
    }
}
