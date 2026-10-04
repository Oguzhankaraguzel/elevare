namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

public sealed record MyTaskSummary(
    int New,
    int InProgress,
    int Waiting,
    int CompletedTotal,
    int CompletedLast7Days,
    int Overdue);
