namespace Application.Features.Queries.Analytics.GetPageViewStats;

/// <summary>Views on one calendar day (UTC), used for the dashboard's bar chart.</summary>
public sealed record DailyViewStat(DateTime Date, int Views);
