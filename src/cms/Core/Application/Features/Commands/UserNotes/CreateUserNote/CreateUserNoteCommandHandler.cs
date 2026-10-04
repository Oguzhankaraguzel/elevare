using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserNotes;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserNotes.CreateUserNote;

internal sealed record CreateUserNoteCommandHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext) : ICommandHandler<CreateUserNoteCommand>
{
    // Not async: the row is only staged here — SaveChangesPipelineBehavior commits
    // it. Marking this async without an await compiles under the .NET 10 preview
    // SDK but is CS1998 on the .NET 9 SDK the project actually targets.
    public Task<Result> Handle(CreateUserNoteCommand request, CancellationToken cancellationToken)
    {
        var note = new UserNote
        {
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            Visibility = request.Visibility,
            Color = request.Color,
            IsPinned = request.IsPinned,
            UserId = UserContext.UserId
        };

        Db.UserNotes.Add(note);

        return Task.FromResult(Result.Success());
    }
}
