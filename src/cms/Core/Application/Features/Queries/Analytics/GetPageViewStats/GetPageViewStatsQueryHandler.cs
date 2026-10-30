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
    private const int MaxSpanDays = 366;

    public async Task<Result<PageViewStatsResponse>> Handle(
        GetPageViewStatsQuery request,
        CancellationToken cancellationToken)
    {
        // The totals and the daily chart cover the same whole UTC days, so they always
        // add up. They used to differ: the totals counted from "now minus N days",
        // which takes in part of a day the chart does not show, and a custom range
        // longer than the chart's cap was still counted in full.
        DateTime firstDay;
        DateTime lastDay;

        if (request.FromUtc.HasValue && request.ToUtc.HasValue)
        {
            firstDay = request.FromUtc.Value.Date;
            lastDay = request.ToUtc.Value.Date;
            if (lastDay < firstDay)
                (firstDay, lastDay) = (lastDay, firstDay);
            if ((lastDay - firstDay).Days + 1 > MaxSpanDays)
                firstDay = lastDay.AddDays(-(MaxSpanDays - 1));
        }
        else
        {
            int days = Math.Clamp(request.Days, 1, 365);
            lastDay = DateTime.UtcNow.Date;
            firstDay = lastDay.AddDays(-(days - 1));
        }

        int spanDays = (lastDay - firstDay).Days + 1;
        DateTime toExclusive = lastDay.AddDays(1);

        IQueryable<PageViewHit> hits = db.PageViewHits
            .AsNoTracking()
            .Where(h => h.ViewedAtUtc >= firstDay && h.ViewedAtUtc < toExclusive);

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
            .Select(offset => firstDay.AddDays(offset))
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
