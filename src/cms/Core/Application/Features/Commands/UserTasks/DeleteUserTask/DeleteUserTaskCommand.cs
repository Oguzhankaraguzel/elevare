using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.UserTasks.DeleteUserTask;

public sealed record DeleteUserTaskCommand(int Id) : ICommand;
