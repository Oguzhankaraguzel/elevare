namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

/// <summary>Traffic for the most recent page the current user published — null if they have none.</summary>
public sealed record LastPublishedPageStats(
    int PageId,
    string Title,
    string FullSlug,
    int TotalViews,
    int UniqueVisitors,
    double AvgDurationSeconds,
    int TotalClicks);
