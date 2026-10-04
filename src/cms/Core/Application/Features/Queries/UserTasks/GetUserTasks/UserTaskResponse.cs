using Domain.Entities.UserTasks;

namespace Application.Features.Queries.UserTasks.GetUserTasks;

public sealed record UserTaskResponse(
    int Id,
    string Title,
    string Description,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    Guid? AssignedByUserId,
    UserTaskStatus Status,
    UserTaskPriority Priority,
    DateTime? StartDate,
    DateTime? DueDate,
    DateTime? CompletedAt,
    bool IsRead,
    bool IsArchived,
    bool IsActive,
    DateTime CreateDate);
