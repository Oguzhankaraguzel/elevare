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

    /// <summary>How many times the reminder e-mail has been tried.</summary>
    public int EmailAttempts { get; set; }

    /// <summary>When a failed e-mail is tried again; null when it is not waiting on a retry.</summary>
    public DateTime? NextEmailAttemptAt { get; set; }

    /// <summary>The e-mail was given up on: no address, or every attempt failed.</summary>
    public bool EmailFailed { get; set; }

    public AppUser User { get; set; } = null!;
}
