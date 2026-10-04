using Domain.Entities.UserReminders;

namespace Application.Features.Queries.UserReminders.GetReminders;

public sealed record ReminderResponse(
    int Id,
    string Title,
    string Message,
    DateTime RemindAt,
    bool IsCompleted,
    bool IsDismissed,
    ReminderChannel Channel,
    Guid UserId,
    bool IsActive,
    DateTime CreateDate);
