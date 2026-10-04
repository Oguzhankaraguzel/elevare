namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

public sealed record UpcomingReminderItem(int Id, string Title, DateTime RemindAt);
