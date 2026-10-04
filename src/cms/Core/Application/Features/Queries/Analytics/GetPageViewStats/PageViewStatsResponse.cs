namespace Application.Features.Queries.Analytics.GetPageViewStats;

public sealed record PageViewStatsResponse(
    int TotalViews,
    int UniqueVisitors,
    int AvgDurationSeconds,
    IReadOnlyList<TopPageStat> TopPages,
    IReadOnlyList<DailyViewStat> Daily);
