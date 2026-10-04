using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.UserTasks.MarkUserTaskAsRead;

public sealed record MarkUserTaskAsReadCommand(int Id) : ICommand;
