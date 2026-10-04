using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserReminders;

namespace Application.Features.Commands.UserReminders.UpdateReminder;

public sealed record UpdateReminderCommand(
    int Id,
    string Title,
    string Message,
    DateTime RemindAt,
    ReminderChannel Channel,
    bool IsCompleted,
    bool IsDismissed,
    bool IsActive) : ICommand;
