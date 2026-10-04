using Application.Abstraction.Services.Authentication;
using Application.Features.Queries.Analytics.GetPageViewStats;
using Domain.Entities.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>Covers the dashboard aggregation: totals, unique visitors, average
/// time-on-page, most-read ordering, period filtering and zero-padded daily series.</summary>
public sealed class GetPageViewStatsQueryHandlerTests
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public GetPageViewStatsQueryHandlerTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            // ApplicationDbContext wraps SaveChanges in a transaction; the InMemory
            // provider doesn't support transactions, so downgrade that to a no-op.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var visitorA = Guid.NewGuid();
        var visitorB = Guid.NewGuid();

        // Past hits are anchored to mid-day of their UTC day, not to "now minus N".
        // The handler buckets by calendar day, so a "now - 1 hour" hit would slide into
        // the PREVIOUS day whenever the suite happens to run between 00:00 and 01:00
        // UTC — a real flake this test used to have. Mid-day keeps every hit
        // unambiguously inside its own day whatever time the suite runs.
        DateTime today = DateTime.UtcNow.Date;
        DateTime now = DateTime.UtcNow;

        using ApplicationDbContext db = CreateDb();
        db.PageViewHits.AddRange(
            // Inside the 7-day window: "/populer" read 3× by 2 visitors.
            new PageViewHit { Path = "/populer", Title = "Popüler", VisitorId = visitorA, ViewedAtUtc = now, DurationSeconds = 60 },
            new PageViewHit { Path = "/populer", Title = "Popüler", VisitorId = visitorB, ViewedAtUtc = today.AddDays(-1).AddHours(12), DurationSeconds = 120 },
            new PageViewHit { Path = "/populer", Title = "Popüler", VisitorId = visitorA, ViewedAtUtc = today.AddDays(-2).AddHours(12), DurationSeconds = null },
            new PageViewHit { Path = "/tekil", Title = "Tekil", VisitorId = visitorA, ViewedAtUtc = today.AddDays(-3).AddHours(12), DurationSeconds = 30 },
            // Outside the 7-day window: must be excluded.
            new PageViewHit { Path = "/eski", Title = "Eski", VisitorId = visitorB, ViewedAtUtc = today.AddDays(-20).AddHours(12), DurationSeconds = 10 });
        db.SaveChanges();
    }

    private ApplicationDbContext CreateDb() => new(_options, new StatsTestUserContext());

    [Fact]
    public async Task Aggregates_totals_top_pages_and_daily_series_for_the_period()
    {
        using ApplicationDbContext db = CreateDb();
        GetPageViewStatsQueryHandler handler = new(db);

        Result<PageViewStatsResponse> result = await handler.Handle(
            new GetPageViewStatsQuery(7), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        PageViewStatsResponse stats = result.Value;

        stats.TotalViews.ShouldBe(4);                 // "/eski" excluded
        stats.UniqueVisitors.ShouldBe(2);
        stats.AvgDurationSeconds.ShouldBe(70);        // (60+120+30)/3 rounded

        stats.TopPages.Count.ShouldBe(2);
        stats.TopPages[0].Path.ShouldBe("/populer");
        stats.TopPages[0].Views.ShouldBe(3);
        stats.TopPages[0].Title.ShouldBe("Popüler");
        stats.TopPages[0].AvgDurationSeconds.ShouldBe(90); // (60+120)/2 — nulls ignored
        stats.TopPages[1].Path.ShouldBe("/tekil");

        stats.Daily.Count.ShouldBe(7);                // zero-padded to the full period
        stats.Daily.Sum(d => d.Views).ShouldBe(4);
        stats.Daily[^1].Date.ShouldBe(DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task Longer_period_includes_older_hits()
    {
        using ApplicationDbContext db = CreateDb();
        GetPageViewStatsQueryHandler handler = new(db);

        Result<PageViewStatsResponse> result = await handler.Handle(
            new GetPageViewStatsQuery(30), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalViews.ShouldBe(5);          // now "/eski" counts too
        result.Value.Daily.Count.ShouldBe(30);
    }

    [Fact]
    public async Task Custom_range_overrides_days_and_excludes_hits_outside_it()
    {
        using ApplicationDbContext db = CreateDb();
        GetPageViewStatsQueryHandler handler = new(db);

        // Whole-day boundaries, exactly like the date-picker UI produces (start-of-day
        // to end-of-day) — this avoids any sub-millisecond race between the seed data's
        // "now" (captured in the constructor) and this method's own DateTime.UtcNow.
        // Window: [-2 days, -1 day] — only the "/populer" hits from -1 and -2 days ago
        // fall inside; -1 hour and -3 days fall outside. Days=999 is deliberately wrong
        // to prove the custom range wins over it.
        DateTime today = DateTime.UtcNow.Date;
        DateTime fromUtc = today.AddDays(-2);
        DateTime toUtc = today.AddDays(-1).AddDays(1).AddTicks(-1); // end of the "-1 day" day

        Result<PageViewStatsResponse> result = await handler.Handle(
            new GetPageViewStatsQuery(999, fromUtc, toUtc), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        PageViewStatsResponse stats = result.Value;

        stats.TotalViews.ShouldBe(2);
        stats.Daily.Count.ShouldBe(2);
        stats.Daily.Sum(d => d.Views).ShouldBe(2);
        stats.Daily[^1].Date.ShouldBe(today.AddDays(-1));
    }

    [Fact]
    public async Task Empty_period_returns_zeroes_not_errors()
    {
        DbContextOptions<ApplicationDbContext> emptyOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using ApplicationDbContext db = new(emptyOptions, new StatsTestUserContext());
        GetPageViewStatsQueryHandler handler = new(db);

        Result<PageViewStatsResponse> result = await handler.Handle(
            new GetPageViewStatsQuery(7), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalViews.ShouldBe(0);
        result.Value.UniqueVisitors.ShouldBe(0);
        result.Value.AvgDurationSeconds.ShouldBe(0);
        result.Value.TopPages.ShouldBeEmpty();
        result.Value.Daily.Count.ShouldBe(7);
    }

    private sealed class StatsTestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => false;
        public bool CanAuthorCustomCode => false;
        public bool HasPermission(string permissionKey) => false;
        public bool IsInRole(string roleName) => false;
    }
}
