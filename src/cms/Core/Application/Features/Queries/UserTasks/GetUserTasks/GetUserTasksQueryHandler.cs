using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserTasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.UserTasks.GetUserTasks;

internal sealed record GetUserTasksQueryHandler(ICmsApplicationDbContext Db)
    : IQueryHandler<GetUserTasksQuery, PagedResult<UserTaskResponse>>
{
    public async Task<Result<PagedResult<UserTaskResponse>>> Handle(
        GetUserTasksQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<UserTask> query = Db.UserTasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Where(t => !t.IsDeleted);

        if (request.Status.HasValue)
            query = query.Where(t => t.Status == request.Status.Value);

        if (request.Priority.HasValue)
            query = query.Where(t => t.Priority == request.Priority.Value);

        if (request.AssignedToUserId.HasValue)
            query = query.Where(t => t.AssignedToUserId == request.AssignedToUserId.Value);

        List<UserTaskResponse> items = await query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreateDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new UserTaskResponse(
                t.Id,
                t.Title,
                t.Description,
                t.AssignedToUserId,
                t.AssignedToUser != null ? t.AssignedToUser.UserName : null,
                t.AssignedByUserId,
                t.Status,
                t.Priority,
                t.StartDate,
                t.DueDate,
                t.CompletedAt,
                t.IsRead,
                t.IsArchived,
                t.IsActive,
                t.CreateDate))
            .ToListAsync(cancellationToken);

        int totalCount = await query.CountAsync(cancellationToken);

        return PagedResult<UserTaskResponse>.Create(items, totalCount, request.Page, request.PageSize);
    }
}
