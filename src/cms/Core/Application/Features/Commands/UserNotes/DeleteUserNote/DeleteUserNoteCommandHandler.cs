using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserNotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserNotes.DeleteUserNote;

internal sealed class DeleteUserNoteCommandHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext) : ICommandHandler<DeleteUserNoteCommand>
{
    public async Task<Result> Handle(DeleteUserNoteCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId = UserContext.UserId;
        UserNote? note = await Db.UserNotes.FirstOrDefaultAsync(
            n => n.Id == request.Id && !n.IsDeleted && n.UserId == currentUserId,
            cancellationToken);

        if (note is null)
            return Result.Failure(UserNoteErrors.Unauthorized);

        Db.UserNotes.Remove(note);

        return Result.Success();
    }
}
