using Application.Abstraction.Services.Authentication;
using Cms.Tests.Support;
using Domain.Entities.UserReminders;
using Domain.Entities.Users;
using Infrastructure.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Data;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// The reminder job used to complete an e-mail reminder whether the e-mail went or
/// not, so a passing mail-server failure lost it without a trace. Now a failed send
/// is retried with growing gaps and given up on, visibly, only after a few tries.
/// </summary>
public sealed class ReminderJobTests
{
    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private readonly RecordingEmailService _email = new();

    private ApplicationDbContext CreateDb() => new(_options, new ReminderTestUserContext());

    private async Task<int> SeedAsync(
        string? email = "ben@example.com",
        ReminderChannel channel = ReminderChannel.Email,
        TimeSpan? late = null,
        int attempts = 0,
        string title = "Yedeği kontrol et")
    {
        using ApplicationDbContext db = CreateDb();
        if (!await db.Users.AnyAsync(u => u.Id == OwnerId))
            db.Users.Add(new AppUser { Id = OwnerId, UserName = "ben", Email = email });
        var reminder = new UserReminder
        {
            UserId = OwnerId,
            Title = title,
            Message = "Pazar günü.",
            RemindAt = DateTime.UtcNow - (late ?? TimeSpan.FromMinutes(1)),
            Channel = channel,
            EmailAttempts = attempts,
        };
        db.UserReminders.Add(reminder);
        await db.SaveChangesAsync();
        return reminder.Id;
    }

    private async Task RunAsync()
    {
        using ApplicationDbContext db = CreateDb();
        await new ReminderJob(db, _email, NullLogger<ReminderJob>.Instance).ExecuteAsync();
    }

    private async Task<UserReminder> LoadAsync(int id)
    {
        using ApplicationDbContext db = CreateDb();
        return await db.UserReminders.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task A_sent_reminder_is_completed()
    {
        int id = await SeedAsync();

        await RunAsync();

        UserReminder reminder = await LoadAsync(id);
        reminder.IsCompleted.ShouldBeTrue();
        reminder.EmailFailed.ShouldBeFalse();
        _email.Sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_failed_send_stays_pending_and_waits_before_trying_again()
    {
        int id = await SeedAsync();
        _email.Configured = false;

        await RunAsync();
        await RunAsync(); // a minute later in real life, but the retry is not due yet

        UserReminder reminder = await LoadAsync(id);
        reminder.IsCompleted.ShouldBeFalse();
        reminder.EmailFailed.ShouldBeFalse();
        reminder.EmailAttempts.ShouldBe(1);
        reminder.NextEmailAttemptAt.ShouldNotBeNull();
        reminder.NextEmailAttemptAt!.Value.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public async Task A_retry_that_succeeds_completes_the_reminder()
    {
        int id = await SeedAsync(attempts: 2);

        await RunAsync();

        UserReminder reminder = await LoadAsync(id);
        reminder.IsCompleted.ShouldBeTrue();
        reminder.EmailAttempts.ShouldBe(3);
        reminder.EmailFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task The_last_failed_attempt_gives_up_visibly()
    {
        int id = await SeedAsync(attempts: 4);
        _email.Configured = false;

        await RunAsync();

        UserReminder reminder = await LoadAsync(id);
        reminder.IsCompleted.ShouldBeTrue();
        reminder.EmailFailed.ShouldBeTrue();
        reminder.EmailAttempts.ShouldBe(5);
    }

    [Fact]
    public async Task A_reminder_a_day_late_is_not_retried()
    {
        int id = await SeedAsync(late: TimeSpan.FromHours(25));
        _email.Configured = false;

        await RunAsync();

        UserReminder reminder = await LoadAsync(id);
        reminder.IsCompleted.ShouldBeTrue();
        reminder.EmailFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Without_an_address_it_gives_up_at_once()
    {
        int id = await SeedAsync(email: null);

        await RunAsync();

        UserReminder reminder = await LoadAsync(id);
        reminder.IsCompleted.ShouldBeTrue();
        reminder.EmailFailed.ShouldBeTrue();
        _email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task In_app_reminders_are_left_for_the_browser()
    {
        int id = await SeedAsync(channel: ReminderChannel.InApp);

        await RunAsync();

        UserReminder reminder = await LoadAsync(id);
        reminder.IsCompleted.ShouldBeFalse();
        reminder.EmailAttempts.ShouldBe(0);
        _email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_title_and_message_are_escaped_in_the_e_mail()
    {
        await SeedAsync(title: "<img src=x onerror=alert(1)>");

        await RunAsync();

        _email.Sent.Single().Body.ShouldNotContain("<img");
        _email.Sent.Single().Body.ShouldContain("&lt;img");
    }

    private sealed class ReminderTestUserContext : IUserContext
    {
        public Guid UserId => OwnerId;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool HasPermission(string permissionKey) => true;
        public bool IsInRole(string roleName) => true;
    }
}
