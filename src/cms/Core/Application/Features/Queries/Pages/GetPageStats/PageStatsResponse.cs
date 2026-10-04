using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPageStats;

public sealed record PageStatsResponse(
    int? SeoScore,
    DateTime? SeoScoreUpdatedAt,
    int TotalViews,
    int UniqueVisitors,
    double AvgDurationSeconds,
    int TotalClicks);
