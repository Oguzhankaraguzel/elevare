using Application.Abstraction.Services.Authentication;
using Application.Features.Queries.Dashboard.GetDashboardOverview;
using Domain.Entities.UserReminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// The home page's "Yaklaşan Hatırlatıcılar" card.
/// <para>
/// The query had no condition on <c>RemindAt</c> at all: it took the five oldest open
/// reminders and called them upcoming. Anything already due therefore sat at the top
/// of the card — and because the list is capped at five, a handful of forgotten
/// reminders pushed every reminder that had not fired yet off the card completely,
/// which is precisely what the card exists to show.
/// </para>
/// </summary>
public sealed class UpcomingRemindersTests
{
    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid StrangerId = Guid.Parse("00000000-0000-0000-0000-0000000000bb");

    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            // ApplicationDbContext wraps SaveChanges in a transaction, which the
            // InMemory provider cannot honour.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private ApplicationDbContext CreateDb() => new(_options, new ReminderTestUserContext());

    private async Task SeedAsync(params UserReminder[] reminders)
    {
        using ApplicationDbContext db = CreateDb();
        db.UserReminders.AddRange(reminders);
        await db.SaveChangesAsync();
    }

    private static UserReminder Reminder(string title, double hoursFromNow, Guid? userId = null) => new()
    {
        UserId = userId ?? OwnerId,
        Title = title,
        Message = title,
        RemindAt = DateTime.UtcNow.AddHours(hoursFromNow),
    };

    private async Task<DashboardOverviewResponse> RunAsync()
    {
        using ApplicationDbContext db = CreateDb();
        Result<DashboardOverviewResponse> result =
            await new GetDashboardOverviewQueryHandler(db, new ReminderTestUserContext())
                .Handle(new GetDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task A_reminder_whose_moment_has_passed_is_not_listed_as_upcoming()
    {
        await SeedAsync(Reminder("dün kaçan", -26), Reminder("yarın", 24));

        DashboardOverviewResponse overview = await RunAsync();

        overview.UpcomingReminders.Select(r => r.Title).ShouldBe(["yarın"]);
    }

    [Fact]
    public async Task Overdue_reminders_are_counted_rather_than_silently_dropped()
    {
        await SeedAsync(Reminder("geçen hafta", -168), Reminder("dün", -26), Reminder("yarın", 24));

        DashboardOverviewResponse overview = await RunAsync();

        overview.OverdueReminders.ShouldBe(2);
    }

    [Fact]
    public async Task Overdue_reminders_cannot_crowd_out_the_ones_still_to_come()
    {
        // The regression in one test: six forgotten reminders used to fill all five
        // slots and hide the one that actually matters.
        await SeedAsync(
            Reminder("eski 1", -100), Reminder("eski 2", -99), Reminder("eski 3", -98),
            Reminder("eski 4", -97), Reminder("eski 5", -96), Reminder("eski 6", -95),
            Reminder("birazdan", 1));

        DashboardOverviewResponse overview = await RunAsync();

        overview.UpcomingReminders.Select(r => r.Title).ShouldContain("birazdan");
        overview.OverdueReminders.ShouldBe(6);
    }

    [Fact]
    public async Task Upcoming_reminders_are_ordered_soonest_first()
    {
        await SeedAsync(Reminder("gelecek hafta", 168), Reminder("birazdan", 1), Reminder("yarın", 24));

        DashboardOverviewResponse overview = await RunAsync();

        overview.UpcomingReminders.Select(r => r.Title).ShouldBe(["birazdan", "yarın", "gelecek hafta"]);
    }

    [Fact]
    public async Task Completed_dismissed_and_other_peoples_reminders_stay_out_of_both_figures()
    {
        using (ApplicationDbContext db = CreateDb())
        {
            UserReminder completed = Reminder("tamamlandı", -5);
            completed.IsCompleted = true;
            UserReminder dismissed = Reminder("kapatıldı", -5);
            dismissed.IsDismissed = true;

            db.UserReminders.AddRange(
                completed,
                dismissed,
                Reminder("başkasının geçmişi", -5, StrangerId),
                Reminder("başkasının geleceği", 5, StrangerId));
            await db.SaveChangesAsync();
        }

        DashboardOverviewResponse overview = await RunAsync();

        overview.UpcomingReminders.ShouldBeEmpty();
        overview.OverdueReminders.ShouldBe(0);
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
