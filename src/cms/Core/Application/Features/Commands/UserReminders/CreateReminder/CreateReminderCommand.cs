using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserReminders;

namespace Application.Features.Commands.UserReminders.CreateReminder;

public sealed record CreateReminderCommand(
    string Title,
    string Message,
    DateTime RemindAt,
    ReminderChannel Channel) : ICommand;
