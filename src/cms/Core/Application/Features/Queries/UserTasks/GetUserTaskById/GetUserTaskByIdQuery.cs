using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.UserTasks.GetUserTaskById;

public sealed record GetUserTaskByIdQuery(int Id) : IQuery<UserTaskDetailResponse>;
