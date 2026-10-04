using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserReminders;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserReminders.DeleteReminder;

internal sealed class DeleteReminderCommandHandler(ICmsApplicationDbContext Db, IUserContext UserContext) : ICommandHandler<DeleteReminderCommand>
{
    public async Task<Result> Handle(DeleteReminderCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId = UserContext.UserId;

        UserReminder? reminder = await Db.UserReminders.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, cancellationToken);

        if (reminder is null)
            return Result.Failure(UserReminderErrors.NotFound);

        if (reminder.UserId != currentUserId)
            return Result.Failure(UserReminderErrors.Unauthorized);

        Db.UserReminders.Remove(reminder);

        return Result.Success();
    }
}
