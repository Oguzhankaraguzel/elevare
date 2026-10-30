using System.Net;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Domain.Entities.UserReminders;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Reminders;

/// <summary>
/// Hangfire job that sends the e-mail for Email-channel reminders whose time has
/// come. Runs every minute as the recurring job "reminder-check". In-app reminders
/// are not its business: they wait for the user in the browser.
/// <para>
/// A reminder is completed only once its e-mail has gone. A failed send is tried
/// again after 1, 2, 4 and 8 minutes; after <see cref="MaxEmailAttempts"/> tries,
/// or a day past its time, the reminder is completed and marked
/// <see cref="UserReminder.EmailFailed"/>. Until 1.3.2 it was completed whether the
/// e-mail went or not, so a passing mail-server hiccup lost it silently.
/// </para>
/// </summary>
public sealed class ReminderJob
{
    private const int MaxEmailAttempts = 5;
    private const int BatchSize = 100;
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromHours(24);

    private readonly ICmsApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(ICmsApplicationDbContext db, IEmailService emailService, ILogger<ReminderJob> logger)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
    }

    // A slow mail server must not let the next minute's run send the same reminders again.
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;

        List<UserReminder> due = await _db.UserReminders
            .Include(r => r.User)
            .Where(r => !r.IsDeleted && !r.IsCompleted && !r.IsDismissed
                        && r.Channel == ReminderChannel.Email
                        && r.RemindAt <= now
                        && (r.NextEmailAttemptAt == null || r.NextEmailAttemptAt <= now))
            .OrderBy(r => r.RemindAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (UserReminder reminder in due)
        {
            await DeliverAsync(reminder, now, cancellationToken);
            // One by one: a crash halfway through must not send the earlier ones twice.
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (due.Count > 0)
            _logger.LogInformation("Reminder check processed {Count} e-mail reminder(s).", due.Count);
    }

    private async Task DeliverAsync(UserReminder reminder, DateTime now, CancellationToken cancellationToken)
    {
        string? email = reminder.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            // Trying again cannot produce an address.
            _logger.LogWarning("Reminder [{Id}] has the Email channel but its user has no e-mail address.", reminder.Id);
            GiveUp(reminder);
            return;
        }

        string body = $"""
            <p>Hello,</p>
            <p>This is a reminder: <strong>{WebUtility.HtmlEncode(reminder.Title)}</strong></p>
            <p>{WebUtility.HtmlEncode(reminder.Message)}</p>
            <p>Scheduled for: {reminder.RemindAt:yyyy-MM-dd HH:mm} UTC</p>
            """;

        Result result = await _emailService.SendAsync(
            EmailMessage.Create(email, $"Reminder: {reminder.Title}", body), cancellationToken);

        reminder.EmailAttempts++;

        if (result.IsSuccess)
        {
            reminder.IsCompleted = true;
            reminder.NextEmailAttemptAt = null;
            _logger.LogInformation("Reminder [{Id}] e-mailed to {Email}.", reminder.Id, email);
            return;
        }

        if (reminder.EmailAttempts >= MaxEmailAttempts || now - reminder.RemindAt > GiveUpAfter)
        {
            _logger.LogError("Reminder [{Id}] could not be e-mailed after {Attempts} attempt(s): {Error}",
                reminder.Id, reminder.EmailAttempts, result.Error?.Description);
            GiveUp(reminder);
            return;
        }

        // 1, 2, 4, 8 minutes: a short outage recovers, a long one is not hammered.
        reminder.NextEmailAttemptAt = now.AddMinutes(Math.Pow(2, reminder.EmailAttempts - 1));
        _logger.LogWarning("Reminder [{Id}] e-mail failed (attempt {Attempt}), trying again at {Next:u}: {Error}",
            reminder.Id, reminder.EmailAttempts, reminder.NextEmailAttemptAt, result.Error?.Description);
    }

    private static void GiveUp(UserReminder reminder)
    {
        reminder.IsCompleted = true;
        reminder.EmailFailed = true;
        reminder.NextEmailAttemptAt = null;
    }
}
