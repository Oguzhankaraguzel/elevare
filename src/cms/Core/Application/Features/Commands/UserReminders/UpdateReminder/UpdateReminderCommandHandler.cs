using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserReminders;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.UserReminders.UpdateReminder;

internal sealed record UpdateReminderCommandHandler(ICmsApplicationDbContext Db, IUserContext UserContext) : ICommandHandler<UpdateReminderCommand>
{
    public async Task<Result> Handle(UpdateReminderCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId = UserContext.UserId;

        UserReminder? reminder = await Db.UserReminders.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, cancellationToken);

        if (reminder is null)
            return Result.Failure(UserReminderErrors.NotFound);

        if (reminder.UserId != currentUserId)
            return Result.Failure(UserReminderErrors.Unauthorized);

        reminder.Title = request.Title;
        reminder.Message = request.Message;
        reminder.RemindAt = request.RemindAt;
        reminder.Channel = request.Channel;
        reminder.IsCompleted = request.IsCompleted;
        reminder.IsDismissed = request.IsDismissed;
        reminder.IsActive = request.IsActive;

        return Result.Success();
    }
}
