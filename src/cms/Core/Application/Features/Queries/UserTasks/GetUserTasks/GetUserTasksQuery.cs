using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using Domain.Entities.UserTasks;

namespace Application.Features.Queries.UserTasks.GetUserTasks;

public sealed record GetUserTasksQuery(
    UserTaskStatus? Status = null,
    UserTaskPriority? Priority = null,
    Guid? AssignedToUserId = null,
    int Page = 1,
    int PageSize = 50)
    : IQuery<PagedResult<UserTaskResponse>>;
