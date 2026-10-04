using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserReminders;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.UserReminders.GetReminders;

internal sealed record GetRemindersQueryHandler(
    ICmsApplicationDbContext Db,
    IUserContext UserContext)
    : IQueryHandler<GetRemindersQuery, PagedResult<ReminderResponse>>
{
    public async Task<Result<PagedResult<ReminderResponse>>> Handle(
        GetRemindersQuery request,
        CancellationToken cancellationToken)
    {
        Guid currentUserId = UserContext.UserId;

        IQueryable<UserReminder> query = Db.UserReminders
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.UserId == currentUserId);

        if (request.IsCompleted.HasValue)
            query = query.Where(r => r.IsCompleted == request.IsCompleted.Value);

        List<ReminderResponse> items = await query
            .OrderBy(r => r.RemindAt)
            .Select(r => new ReminderResponse(
                r.Id,
                r.Title,
                r.Message,
                r.RemindAt,
                r.IsCompleted,
                r.IsDismissed,
                r.Channel,
                r.UserId,
                r.IsActive,
                r.CreateDate))
            .ToListAsync(cancellationToken);

        return PagedResult<ReminderResponse>.Create(items, items.Count, 1, items.Count);
    }
}
