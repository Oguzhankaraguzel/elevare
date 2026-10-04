using Domain.Entities.Abstractions;
using Domain.Entities.Users;

namespace Domain.Entities.UserReminders;

public class UserReminder : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTime RemindAt { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsDismissed { get; set; }
    public ReminderChannel Channel { get; set; } = ReminderChannel.InApp;

    public AppUser User { get; set; } = null!;
}
