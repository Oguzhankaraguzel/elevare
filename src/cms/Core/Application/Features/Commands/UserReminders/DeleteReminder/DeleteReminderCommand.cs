using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.UserReminders.DeleteReminder;

public sealed record DeleteReminderCommand(int Id) : ICommand;
