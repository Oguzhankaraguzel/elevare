using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;

namespace Application.Features.Commands.UserTasks.UpdateUserTask;

public sealed record UpdateUserTaskCommand(
    int Id,
    string Title,
    string Description,
    Guid? AssignedToUserId,
    UserTaskStatus Status,
    UserTaskPriority Priority,
    DateTime? StartDate,
    DateTime? DueDate,
    bool IsArchived,
    bool IsActive) : ICommand;
