using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.UserReminders.GetReminders;

public sealed record GetRemindersQuery(bool? IsCompleted = null)
    : IQuery<PagedResult<ReminderResponse>>;
