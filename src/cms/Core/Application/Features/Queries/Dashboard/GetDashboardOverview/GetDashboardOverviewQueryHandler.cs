using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Features.Queries.Pages.GetPageStats;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.Permissions;
using Domain.Entities.UserTasks;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

internal sealed class GetDashboardOverviewQueryHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : IQueryHandler<GetDashboardOverviewQuery, DashboardOverviewResponse>
{
    private const int UpcomingReminderCount = 5;
    private const int WorkflowQueueCount = 5;
    private const int RecentActivityCount = 5;

    public async Task<Result<DashboardOverviewResponse>> Handle(
        GetDashboardOverviewQuery request,
        CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;
        DateTime nowUtc = DateTime.UtcNow;
        DateTime sevenDaysAgoUtc = nowUtc.AddDays(-7);

        Dictionary<UserTaskStatus, int> byStatus = await db.UserTasks
            .AsNoTracking()
            .Where(t => !t.IsDeleted && !t.IsArchived && t.AssignedToUserId == userId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        int overdue = await db.UserTasks
            .AsNoTracking()
            .CountAsync(t => !t.IsDeleted && !t.IsArchived && t.AssignedToUserId == userId
                && t.DueDate != null && t.DueDate < nowUtc
                && t.Status != UserTaskStatus.Completed
                && t.Status != UserTaskStatus.Cancelled
                && t.Status != UserTaskStatus.Rejected,
                cancellationToken);

        int completedLast7Days = await db.UserTasks
            .AsNoTracking()
            .CountAsync(t => !t.IsDeleted && t.AssignedToUserId == userId
                && t.Status == UserTaskStatus.Completed
                && t.CompletedAt != null && t.CompletedAt >= sevenDaysAgoUtc,
                cancellationToken);

        var myTasks = new MyTaskSummary(
            New: byStatus.GetValueOrDefault(UserTaskStatus.New),
            InProgress: byStatus.GetValueOrDefault(UserTaskStatus.InProgress),
            Waiting: byStatus.GetValueOrDefault(UserTaskStatus.Waiting),
            CompletedTotal: byStatus.GetValueOrDefault(UserTaskStatus.Completed),
            CompletedLast7Days: completedLast7Days,
            Overdue: overdue);

        // "Upcoming" has to mean upcoming. Without the RemindAt filter this was simply
        // the oldest open reminders, so anything already due sat at the top of a card
        // headed "Yaklaşan" — and once five of them had built up, Take() filled the list
        // with them and the reminders that had not fired yet fell off entirely.
        List<UpcomingReminderItem> upcomingReminders = await db.UserReminders
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.UserId == userId && !r.IsCompleted && !r.IsDismissed
                && r.RemindAt >= nowUtc)
            .OrderBy(r => r.RemindAt)
            .Take(UpcomingReminderCount)
            .Select(r => new UpcomingReminderItem(r.Id, r.Title, r.RemindAt))
            .ToListAsync(cancellationToken);

        // Counted rather than dropped: a reminder that already fired and was never acted
        // on is exactly the one worth chasing. Surfaced as a badge, mirroring how overdue
        // tasks are reported above.
        int overdueReminders = await db.UserReminders
            .AsNoTracking()
            .CountAsync(r => !r.IsDeleted && r.UserId == userId && !r.IsCompleted && !r.IsDismissed
                && r.RemindAt < nowUtc,
                cancellationToken);

        int pinnedNotes = await db.UserNotes
            .AsNoTracking()
            .CountAsync(n => !n.IsDeleted && n.UserId == userId && !n.IsArchived && n.IsPinned, cancellationToken);
        int totalNotes = await db.UserNotes
            .AsNoTracking()
            .CountAsync(n => !n.IsDeleted && n.UserId == userId && !n.IsArchived, cancellationToken);
        var myNotes = new MyNotesSummary(pinnedNotes, totalNotes);

        Dictionary<PageStatus, int> byPageStatus = await db.PageInfos
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.PageStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var contentHealth = new ContentHealthSummary(
            Draft: byPageStatus.GetValueOrDefault(PageStatus.Draft),
            Published: byPageStatus.GetValueOrDefault(PageStatus.Published),
            Archived: byPageStatus.GetValueOrDefault(PageStatus.Archived));

        List<WorkflowQueueItem> myPendingApprovals = await GetMyPendingApprovalsAsync(db, userContext, cancellationToken);
        List<WorkflowQueueItem> mySubmittedContent = await GetMySubmittedContentAsync(db, userId, cancellationToken);
        List<RecentActivityItem> myRecentActivity = await GetMyRecentActivityAsync(db, userId, cancellationToken);
        LastPublishedPageStats? lastPublished = await GetLastPublishedPageStatsAsync(db, userId, cancellationToken);

        return Result.Success(new DashboardOverviewResponse(
            myTasks, upcomingReminders, overdueReminders, myNotes, contentHealth,
            myPendingApprovals, mySubmittedContent, myRecentActivity, lastPublished));
    }

    /// <summary>
    /// Requests parked at a step this user can decide. Gated on <c>Approvals.Decide</c>
    /// the same way <c>GetPendingApprovalsQuery</c> is — a user without it never holds
    /// any step, so the query would return nothing anyway; checking first just skips
    /// the round trip for the common case (most editors don't have this permission).
    /// </summary>
    private static async Task<List<WorkflowQueueItem>> GetMyPendingApprovalsAsync(
        ICmsApplicationDbContext db, IUserContext userContext, CancellationToken cancellationToken)
    {
        if (!userContext.IsAdminOrAbove && !userContext.HasPermission(PermissionKeys.ApprovalsDecide))
            return [];

        List<ApprovalRequest> pending = await db.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending)
            .Include(a => a.WorkflowDefinition).ThenInclude(w => w.Steps).ThenInclude(s => s.RequiredRole)
            .Include(a => a.WorkflowDefinition).ThenInclude(w => w.Steps).ThenInclude(s => s.RequiredUser)
            .OrderBy(a => a.CreateDate)
            .ToListAsync(cancellationToken);

        // Mirrors DecideApprovalCommandHandler.IsAllowedToDecide: a step naming a
        // specific person belongs to them alone, otherwise anyone in the role can act.
        List<ApprovalRequest> mine = [.. pending.Where(a =>
        {
            WorkflowStep? step = a.WorkflowDefinition.Steps.FirstOrDefault(s => s.StepOrder == a.CurrentStepOrder);
            if (step is null) return false;

            return step.RequiredUserId is { } requiredUserId
                ? userContext.UserId == requiredUserId
                : userContext.IsAdminOrAbove || userContext.IsInRole(step.RequiredRole.Name ?? "");
        }).Take(WorkflowQueueCount)];

        return await ToWorkflowQueueItemsAsync(db, mine, cancellationToken);
    }

    /// <summary>Requests this user submitted that are still working their way through a chain.</summary>
    private static async Task<List<WorkflowQueueItem>> GetMySubmittedContentAsync(
        ICmsApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        List<ApprovalRequest> mine = await db.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.CreateUserId == userId)
            .Include(a => a.WorkflowDefinition).ThenInclude(w => w.Steps).ThenInclude(s => s.RequiredRole)
            .OrderByDescending(a => a.CreateDate)
            .Take(WorkflowQueueCount)
            .ToListAsync(cancellationToken);

        return await ToWorkflowQueueItemsAsync(db, mine, cancellationToken);
    }

    private static async Task<List<WorkflowQueueItem>> ToWorkflowQueueItemsAsync(
        ICmsApplicationDbContext db, List<ApprovalRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return [];

        List<int> pageIds = [.. requests.Where(a => a.ContentType == WorkflowContentType.Page).Select(a => a.ContentId)];
        List<int> templateIds = [.. requests.Where(a => a.ContentType == WorkflowContentType.PageTemplate).Select(a => a.ContentId)];

        Dictionary<int, string> pageTitles = await db.PageInfos
            .Where(p => pageIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.SeoMeta.Title.HasValue() ? p.SeoMeta.Title : p.Slug, cancellationToken);

        Dictionary<int, string> templateNames = await db.PageTemplates
            .Where(t => templateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return [.. requests.Select(a =>
        {
            WorkflowStep? step = a.WorkflowDefinition.Steps.FirstOrDefault(s => s.StepOrder == a.CurrentStepOrder);
            string title = a.ContentType == WorkflowContentType.Page
                ? pageTitles.GetValueOrDefault(a.ContentId, "—")
                : templateNames.GetValueOrDefault(a.ContentId, "—");

            return new WorkflowQueueItem(
                a.Id, a.ContentType, a.ContentId, title, a.WorkflowDefinition.Name,
                a.CurrentStepOrder, a.WorkflowDefinition.Steps.Count, step?.RequiredRole.Name ?? "—");
        })];
    }

    /// <summary>The pages and templates this user most recently saved, newest first.</summary>
    private static async Task<List<RecentActivityItem>> GetMyRecentActivityAsync(
        ICmsApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        List<RecentActivityItem> pages = await db.PageInfos
            .Where(p => !p.IsDeleted && p.UpdateUserId == userId && p.UpdateDate != null)
            .OrderByDescending(p => p.UpdateDate)
            .Take(RecentActivityCount)
            .Select(p => new RecentActivityItem(
                WorkflowContentType.Page, p.Id,
                p.SeoMeta.Title.HasValue() ? p.SeoMeta.Title : p.Slug,
                p.UpdateDate!.Value, p.PageStatus))
            .ToListAsync(cancellationToken);

        List<RecentActivityItem> templates = await db.PageTemplates
            .Where(t => !t.IsDeleted && t.UpdateUserId == userId && t.UpdateDate != null)
            .OrderByDescending(t => t.UpdateDate)
            .Take(RecentActivityCount)
            .Select(t => new RecentActivityItem(WorkflowContentType.PageTemplate, t.Id, t.Name, t.UpdateDate!.Value, null))
            .ToListAsync(cancellationToken);

        return [.. pages.Concat(templates).OrderByDescending(a => a.UpdateDate).Take(RecentActivityCount)];
    }

    /// <summary>
    /// Traffic for the page this user most recently brought to Published. Reuses
    /// <see cref="GetPageStatsQueryHandler.BuildPathCandidates"/> so a slug change or
    /// the reserved "home" slug is resolved exactly the same way the Pages list's own
    /// stats popover resolves it.
    /// </summary>
    private static async Task<LastPublishedPageStats?> GetLastPublishedPageStatsAsync(
        ICmsApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        PageInfo? page = await db.PageInfos
            .Include(p => p.Language)
            .Where(p => !p.IsDeleted && p.UpdateUserId == userId && p.PageStatus == PageStatus.Published)
            .OrderByDescending(p => p.UpdateDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (page is null)
            return null;

        List<string> paths = GetPageStatsQueryHandler.BuildPathCandidates(page);

        IQueryable<Domain.Entities.Analytics.PageViewHit> hits = db.PageViewHits.Where(h => paths.Contains(h.Path));

        int totalViews = await hits.CountAsync(cancellationToken);
        int uniqueVisitors = await hits.Select(h => h.VisitorId).Distinct().CountAsync(cancellationToken);
        double avgDuration = await hits
            .Where(h => h.DurationSeconds != null)
            .AverageAsync(h => (double?)h.DurationSeconds, cancellationToken) ?? 0;
        int totalClicks = await db.PageClickHits.CountAsync(h => paths.Contains(h.Path), cancellationToken);

        string title = page.SeoMeta.Title.HasValue() ? page.SeoMeta.Title : page.Slug;
        return new LastPublishedPageStats(page.Id, title, page.FullSlug, totalViews, uniqueVisitors, avgDuration, totalClicks);
    }
}
