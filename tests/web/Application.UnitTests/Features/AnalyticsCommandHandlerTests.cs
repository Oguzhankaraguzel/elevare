using Application.Features.Commands.Analytics.RecordPageDuration;
using Application.Features.Commands.Analytics.RecordPageView;
using Domain.Entities.PublicAnalytics;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>Covers the two telemetry writes: recording a page view and later
/// attaching the time-on-page reported by the browser's leave-beacon.</summary>
public sealed class AnalyticsCommandHandlerTests
{
    private readonly DbContextOptions<AnalyticsDbContext> _options =
        new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private AnalyticsDbContext CreateDb() => new(_options);

    [Fact]
    public async Task Recording_a_view_stores_the_hit_and_returns_its_id()
    {
        var visitorId = Guid.NewGuid();
        using AnalyticsDbContext db = CreateDb();
        RecordPageViewCommandHandler handler = new(db);

        Result<long> result = await handler.Handle(
            new RecordPageViewCommand("/hakkimizda", "Hakkımızda", visitorId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeGreaterThan(0);

        PublicPageViewHit hit = await db.PageViewHits.SingleAsync(CancellationToken.None);
        hit.Path.ShouldBe("/hakkimizda");
        hit.Title.ShouldBe("Hakkımızda");
        hit.VisitorId.ShouldBe(visitorId);
        hit.DurationSeconds.ShouldBeNull();
        hit.ViewedAtUtc.ShouldBeInRange(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Overlong_path_and_title_are_truncated_to_column_limits()
    {
        using AnalyticsDbContext db = CreateDb();
        RecordPageViewCommandHandler handler = new(db);

        string longPath = "/" + new string('a', 600);
        string longTitle = new string('b', 400);

        Result<long> result = await handler.Handle(
            new RecordPageViewCommand(longPath, longTitle, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        PublicPageViewHit hit = await db.PageViewHits.SingleAsync(CancellationToken.None);
        hit.Path.Length.ShouldBe(500);
        hit.Title.ShouldNotBeNull();
        hit.Title.Length.ShouldBe(300);
    }

    [Fact]
    public async Task Empty_path_is_rejected()
    {
        using AnalyticsDbContext db = CreateDb();
        RecordPageViewCommandHandler handler = new(db);

        Result<long> result = await handler.Handle(
            new RecordPageViewCommand("  ", null, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        (await db.PageViewHits.CountAsync(CancellationToken.None)).ShouldBe(0);
    }

    [Fact]
    public async Task Duration_beacon_updates_the_recorded_hit()
    {
        long hitId = await SeedHitAsync();

        using AnalyticsDbContext db = CreateDb();
        RecordPageDurationCommandHandler handler = new(db);
        Result result = await handler.Handle(new RecordPageDurationCommand(hitId, 42), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await db.PageViewHits.SingleAsync(CancellationToken.None)).DurationSeconds.ShouldBe(42);
    }

    [Fact]
    public async Task Duration_keeps_the_larger_value_when_beacons_race()
    {
        long hitId = await SeedHitAsync(durationSeconds: 90);

        using AnalyticsDbContext db = CreateDb();
        RecordPageDurationCommandHandler handler = new(db);
        await handler.Handle(new RecordPageDurationCommand(hitId, 30), CancellationToken.None);

        (await db.PageViewHits.SingleAsync(CancellationToken.None)).DurationSeconds.ShouldBe(90);
    }

    [Fact]
    public async Task Duration_is_capped_at_four_hours()
    {
        long hitId = await SeedHitAsync();

        using AnalyticsDbContext db = CreateDb();
        RecordPageDurationCommandHandler handler = new(db);
        await handler.Handle(new RecordPageDurationCommand(hitId, 999_999), CancellationToken.None);

        (await db.PageViewHits.SingleAsync(CancellationToken.None)).DurationSeconds.ShouldBe(4 * 60 * 60);
    }

    [Theory]
    [InlineData(0, 10)]     // bogus id
    [InlineData(12345, 10)] // unknown id
    [InlineData(1, -5)]     // negative duration
    public async Task Invalid_duration_beacons_are_silent_no_ops(long hitId, int seconds)
    {
        await SeedHitAsync();

        using AnalyticsDbContext db = CreateDb();
        RecordPageDurationCommandHandler handler = new(db);
        Result result = await handler.Handle(new RecordPageDurationCommand(hitId, seconds), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await db.PageViewHits.SingleAsync(CancellationToken.None)).DurationSeconds.ShouldBeNull();
    }

    private async Task<long> SeedHitAsync(int? durationSeconds = null)
    {
        using AnalyticsDbContext db = CreateDb();
        var hit = new PublicPageViewHit
        {
            Path = "/test",
            VisitorId = Guid.NewGuid(),
            ViewedAtUtc = DateTime.UtcNow,
            DurationSeconds = durationSeconds
        };
        db.PageViewHits.Add(hit);
        await db.SaveChangesAsync(CancellationToken.None);
        return hit.Id;
    }
}
