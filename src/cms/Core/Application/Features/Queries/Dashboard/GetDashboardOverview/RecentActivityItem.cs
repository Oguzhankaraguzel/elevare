using Domain.Entities.PageInfos;
using Domain.Entities.Workflows;

namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

/// <summary>
/// One page or template the current user last touched. <see cref="Status"/> is null
/// for templates — they have no publish state of their own.
/// </summary>
public sealed record RecentActivityItem(
    WorkflowContentType ContentType,
    int ContentId,
    string Title,
    DateTime UpdateDate,
    PageStatus? Status);
