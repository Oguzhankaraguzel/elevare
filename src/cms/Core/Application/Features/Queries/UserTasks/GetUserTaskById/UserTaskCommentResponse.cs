using Domain.Entities.UserTasks;

namespace Application.Features.Queries.UserTasks.GetUserTaskById;

public sealed record UserTaskCommentResponse(
    int Id,
    Guid UserId,
    string? UserName,
    string Comment,
    DateTime CreateDate);
