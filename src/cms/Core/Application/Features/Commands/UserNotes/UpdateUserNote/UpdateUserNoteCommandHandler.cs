using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserNotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserNotes.UpdateUserNote;

internal sealed record UpdateUserNoteCommandHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext) : ICommandHandler<UpdateUserNoteCommand>
{
    public async Task<Result> Handle(UpdateUserNoteCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId = UserContext.UserId;
        UserNote? note = await Db.UserNotes.FirstOrDefaultAsync(
            n => n.Id == request.Id && !n.IsDeleted && n.UserId == currentUserId,
            cancellationToken);

        if (note is null)
            return Result.Failure(UserNoteErrors.Unauthorized);

        note.Title = request.Title;
        note.Content = request.Content;
        note.Type = request.Type;
        note.Visibility = request.Visibility;
        note.Color = request.Color;
        note.IsPinned = request.IsPinned;
        note.IsArchived = request.IsArchived;
        note.IsActive = request.IsActive;

        return Result.Success();
    }
}
