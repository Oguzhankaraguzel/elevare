using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;

namespace Application.Features.Commands.UserTasks.CreateUserTask;

public sealed record CreateUserTaskCommand(
    string Title,
    string Description,
    Guid? AssignedToUserId,
    UserTaskPriority Priority,
    DateTime? StartDate,
    DateTime? DueDate,
    int? ParentTaskId) : ICommand;
