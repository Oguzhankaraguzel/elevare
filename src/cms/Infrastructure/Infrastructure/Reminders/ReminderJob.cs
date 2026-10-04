using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Domain.Entities.UserReminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Reminders;

/// <summary>
/// Hangfire job that checks for pending reminders every minute.
/// Finds reminders where RemindAt &lt;= now and IsCompleted == false,
/// marks them as completed, and sends email for Email-channel reminders.
/// Registered as a recurring job with key "reminder-check".
/// </summary>
public sealed class ReminderJob
{
    private readonly ICmsApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(ICmsApplicationDbContext db, IEmailService emailService, ILogger<ReminderJob> logger)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reminder check started.");

        DateTime now = DateTime.UtcNow;

        List<UserReminder> pendingReminders = await _db.UserReminders
            .Include(r => r.User)
            .Where(r => !r.IsDeleted && !r.IsCompleted && !r.IsDismissed && r.RemindAt <= now)
            .ToListAsync(cancellationToken);

        if (pendingReminders.Count == 0)
        {
            _logger.LogInformation("No pending reminders found.");
            return;
        }

        foreach (UserReminder reminder in pendingReminders)
        {
            _logger.LogInformation(
                "Reminder triggered: [{Id}] \"{Title}\" for user {UserId} (Channel: {Channel})",
                reminder.Id, reminder.Title, reminder.UserId, reminder.Channel);

            if (reminder.Channel == ReminderChannel.Email)
            {
                string? userEmail = reminder.User?.Email;
                if (!string.IsNullOrWhiteSpace(userEmail))
                {
                    string subject = $"Reminder: {reminder.Title}";
                    string body = $"""
                        <p>Hello,</p>
                        <p>This is a reminder: <strong>{reminder.Title}</strong></p>
                        <p>{reminder.Message}</p>
                        <p>Scheduled for: {reminder.RemindAt:yyyy-MM-dd HH:mm} UTC</p>
                        """;

                    Result emailResult = await _emailService.SendAsync(
                        EmailMessage.Create(userEmail, subject, body),
                        cancellationToken);

                    if (emailResult.IsSuccess)
                        _logger.LogInformation("Reminder email sent to {Email} for reminder [{Id}].", userEmail, reminder.Id);
                    else
                        _logger.LogWarning("Failed to send reminder email for [{Id}]: {Error}", reminder.Id, emailResult.Error?.Description);
                }
                else
                {
                    _logger.LogWarning("Reminder [{Id}] has Email channel but user email is not available.", reminder.Id);
                }

                reminder.IsCompleted = true;
            }
            // InApp reminders stay pending until the user acknowledges them in the browser.
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reminder check completed. {Count} reminder(s) triggered.", pendingReminders.Count);
    }
}
