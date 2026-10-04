using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserReminders;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserReminders.CreateReminder;

internal sealed record CreateReminderCommandHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext) : ICommandHandler<CreateReminderCommand>
{
    // Not async: the row is only staged here — SaveChangesPipelineBehavior commits
    // it. Marking this async without an await compiles under the .NET 10 preview
    // SDK but is CS1998 on the .NET 9 SDK the project actually targets.
    public Task<Result> Handle(CreateReminderCommand request, CancellationToken cancellationToken)
    {
        var reminder = new UserReminder
        {
            Title = request.Title,
            Message = request.Message,
            RemindAt = request.RemindAt,
            Channel = request.Channel,
            UserId = UserContext.UserId
        };

        Db.UserReminders.Add(reminder);

        return Task.FromResult(Result.Success());
    }
}
