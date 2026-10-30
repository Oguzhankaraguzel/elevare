using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Analytics.GetPageViewStats;

/// <summary>
/// Aggregates public-site page views for the CMS dashboard: totals, unique
/// visitors, average time-on-page, most-read pages and a per-day series.
/// <para>
/// Two modes: pass only <paramref name="Days"/> (clamped to 1–365) for the
/// quick-select shortcuts — today, the last 7 or 30 days, today included; or pass
/// both <paramref name="FromUtc"/> and <paramref name="ToUtc"/> for an explicit
/// custom range of days (both included, at most 366), in which case
/// <paramref name="Days"/> is ignored. Either way whole UTC days are counted, the
/// same days the daily series shows.
/// </para>
/// </summary>
public sealed record GetPageViewStatsQuery(int Days, DateTime? FromUtc = null, DateTime? ToUtc = null)
    : IQuery<PageViewStatsResponse>;
