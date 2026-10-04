using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;

namespace Application.Features.Commands.UserTasks.UpdateUserTaskStatus;

public sealed record UpdateUserTaskStatusCommand(int Id, UserTaskStatus Status) : ICommand;
