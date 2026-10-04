using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.UserTasks.GetUserTaskById;

internal sealed record GetUserTaskByIdQueryHandler(ICmsApplicationDbContext Db)
    : IQueryHandler<GetUserTaskByIdQuery, UserTaskDetailResponse>
{
    public async Task<Result<UserTaskDetailResponse>> Handle(
        GetUserTaskByIdQuery request,
        CancellationToken cancellationToken)
    {
        UserTask? task = await Db.UserTasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.Comments)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken);

        if (task is null)
            return Result.Failure<UserTaskDetailResponse>(UserTaskErrors.NotFound);

        var response = new UserTaskDetailResponse(
            task.Id,
            task.Title,
            task.Description,
            task.AssignedToUserId,
            task.AssignedToUser?.UserName,
            task.AssignedByUserId,
            task.Status,
            task.Priority,
            task.StartDate,
            task.DueDate,
            task.CompletedAt,
            task.IsRead,
            task.IsArchived,
            task.IsActive,
            task.CreateDate,
            task.Comments
                .OrderBy(c => c.CreateDate)
                .Select(c => new UserTaskCommentResponse(
                    c.Id,
                    c.UserId,
                    c.User?.UserName,
                    c.Comment,
                    c.CreateDate))
                .ToList());

        return Result.Success(response);
    }
}
