using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Analytics.GetPageViewStats;

/// <summary>
/// Aggregates public-site page views for the CMS dashboard: totals, unique
/// visitors, average time-on-page, most-read pages and a per-day series.
/// <para>
/// Two modes: pass only <paramref name="Days"/> (clamped to 1–365) for the
/// quick-select shortcuts (last 24h/7d/30d, ending "now"); or pass both
/// <paramref name="FromUtc"/> and <paramref name="ToUtc"/> for an explicit
/// custom date range, in which case <paramref name="Days"/> is ignored.
/// </para>
/// </summary>
public sealed record GetPageViewStatsQuery(int Days, DateTime? FromUtc = null, DateTime? ToUtc = null)
    : IQuery<PageViewStatsResponse>;
