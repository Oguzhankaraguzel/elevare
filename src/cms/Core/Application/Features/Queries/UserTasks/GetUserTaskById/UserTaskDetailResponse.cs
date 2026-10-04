using Domain.Entities.UserTasks;

namespace Application.Features.Queries.UserTasks.GetUserTaskById;

public sealed record UserTaskDetailResponse(
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
    DateTime CreateDate,
    IReadOnlyList<UserTaskCommentResponse> Comments);
