namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

public sealed record DashboardOverviewResponse(
    MyTaskSummary MyTasks,
    List<UpcomingReminderItem> UpcomingReminders,
    /// <summary>Open reminders whose moment has already passed — shown as a badge, not in the list.</summary>
    int OverdueReminders,
    MyNotesSummary MyNotes,
    ContentHealthSummary ContentHealth,
    List<WorkflowQueueItem> MyPendingApprovals,
    List<WorkflowQueueItem> MySubmittedContent,
    List<RecentActivityItem> MyRecentActivity,
    LastPublishedPageStats? LastPublishedPageStats);
