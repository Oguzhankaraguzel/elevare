using Application.Abstraction.Data;
using Domain.Entities.Analytics;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Analytics.GetPageViewStats;

internal sealed class GetPageViewStatsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageViewStatsQuery, PageViewStatsResponse>
{
    private const int TopPageCount = 10;

    public async Task<Result<PageViewStatsResponse>> Handle(
        GetPageViewStatsQuery request,
        CancellationToken cancellationToken)
    {
        bool customRange = request.FromUtc.HasValue && request.ToUtc.HasValue;

        DateTime fromUtc;
        DateTime today; // anchors the zero-padded daily series below
        int spanDays;

        if (customRange)
        {
            fromUtc = request.FromUtc!.Value;
            today = request.ToUtc!.Value.Date;
            spanDays = Math.Clamp((today - fromUtc.Date).Days + 1, 1, 366);
        }
        else
        {
            int days = Math.Clamp(request.Days, 1, 365);
            fromUtc = DateTime.UtcNow.AddDays(-days);
            today = DateTime.UtcNow.Date;
            spanDays = days;
        }

        IQueryable<PageViewHit> hits = db.PageViewHits
            .AsNoTracking()
            .Where(h => h.ViewedAtUtc >= fromUtc);

        if (customRange)
            hits = hits.Where(h => h.ViewedAtUtc <= request.ToUtc!.Value);

        int totalViews = await hits.CountAsync(cancellationToken);

        int uniqueVisitors = await hits
            .Select(h => h.VisitorId)
            .Distinct()
            .CountAsync(cancellationToken);

        double avgDuration = await hits
            .Where(h => h.DurationSeconds != null)
            .AverageAsync(h => (double?)h.DurationSeconds, cancellationToken) ?? 0;

        var topPages = (await hits
            .GroupBy(h => h.Path)
            .Select(g => new
            {
                Path = g.Key,
                Title = g.Max(h => h.Title),
                Views = g.Count(),
                AvgDuration = g.Average(h => (double?)h.DurationSeconds) ?? 0
            })
            .OrderByDescending(g => g.Views)
            .Take(TopPageCount)
            .ToListAsync(cancellationToken))
            .Select(g => new TopPageStat(g.Path, g.Title, g.Views, (int)Math.Round(g.AvgDuration)))
            .ToList();

        Dictionary<DateTime, int> byDay = await hits
            .GroupBy(h => h.ViewedAtUtc.Date)
            .Select(g => new { Date = g.Key, Views = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Views, cancellationToken);

        // Pad the series with zero-days so the chart always spans the whole period.
        var daily = Enumerable.Range(0, spanDays)
            .Select(offset => today.AddDays(-(spanDays - 1 - offset)))
            .Select(date => new DailyViewStat(date, byDay.TryGetValue(date, out int views) ? views : 0))
            .ToList();

        var response = new PageViewStatsResponse(
            totalViews,
            uniqueVisitors,
            (int)Math.Round(avgDuration),
            topPages,
            daily);

        return Result.Success(response);
    }
}
