using Domain.Entities.Analytics;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.FormReplyTemplates;
using Domain.Entities.FormSubmissions;
using Domain.Entities.IntegrationSecrets;
using Domain.Entities.Languages;
using Domain.Entities.Logs;
using Domain.Entities.Media;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.Redirects;
using Domain.Entities.Sitemaps;
using Domain.Entities.SiteCodeSnippets;
using Domain.Entities.SiteSettings;
using Domain.Entities.Tags;
using Domain.Entities.UserNotes;
using Domain.Entities.UserReminders;
using Domain.Entities.UserTasks;
using Domain.Entities.Users;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstraction.Data;

public interface ICmsApplicationDbContext
{
    #region Tables
    DbSet<PageInfo> PageInfos { get; set; }
    DbSet<PageContent> PageContents { get; set; }
    DbSet<PageTemplate> PageTemplates { get; set; }
    DbSet<PageGroup> PageGroups { get; set; }
    DbSet<Language> Languages { get; set; }
    DbSet<AppUser> AppUsers { get; set; }
    DbSet<AppRole> AppRoles { get; set; }
    DbSet<MediaFile> MediaFiles { get; set; }
    DbSet<SiteSetting> SiteSettings { get; set; }
    DbSet<IntegrationSecret> IntegrationSecrets { get; set; }
    DbSet<SitemapCache> SitemapCaches { get; set; }
    DbSet<UserNote> UserNotes { get; set; }
    DbSet<AnnouncementRead> AnnouncementReads { get; set; }
    DbSet<UserReminder> UserReminders { get; set; }
    DbSet<UserTask> UserTasks { get; set; }
    DbSet<UserTaskComment> UserTaskComments { get; set; }
    DbSet<PageViewHit> PageViewHits { get; set; }
    DbSet<PageClickHit> PageClickHits { get; set; }
    DbSet<AppLog> AppLogs { get; set; }
    DbSet<ContentBulkEdit> ContentBulkEdits { get; set; }
    DbSet<ContentBulkEditItem> ContentBulkEditItems { get; set; }
    DbSet<FormSubmission> FormSubmissions { get; set; }
    DbSet<FormSubmissionAttachment> FormSubmissionAttachments { get; set; }
    DbSet<FormReplyTemplate> FormReplyTemplates { get; set; }
    DbSet<Redirect> Redirects { get; set; }
    DbSet<SiteCodeSnippet> SiteCodeSnippets { get; set; }
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    DbSet<WorkflowStep> WorkflowSteps { get; set; }
    DbSet<ApprovalRequest> ApprovalRequests { get; set; }
    DbSet<ApprovalStepDecision> ApprovalStepDecisions { get; set; }
    DbSet<Tag> Tags { get; set; }
    DbSet<PageInfoTag> PageInfoTags { get; set; }
    DbSet<PageInfoSiteCodeExclusion> PageInfoSiteCodeExclusions { get; set; }
    DbSet<AuthEvent> AuthEvents { get; set; }
    DbSet<PasswordSetupToken> PasswordSetupTokens { get; set; }
    #endregion

    /// <summary>
    /// Marks an entity for REAL deletion on the next save, bypassing the soft-delete
    /// interceptor that would otherwise turn it back into <c>IsDeleted = true</c>.
    /// <para>
    /// The only legitimate caller is the Trash's permanent delete: everywhere else,
    /// turning a Remove into a soft delete is exactly the behaviour we want. Kept as
    /// an explicit method rather than a flag so a hard delete is impossible to write
    /// by accident and greppable when reviewing.
    /// </para>
    /// </summary>
    void RemovePermanently<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesWithRetryAsync(int retryCount = 3, CancellationToken cancellationToken = default);
    Task<bool> EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default);

    int SaveChanges(CancellationToken cancellationToken = default);
    int SaveChangesWithRetry(int retryCount = 3, CancellationToken cancellationToken = default);
    bool EnsureDatabaseCreated(CancellationToken cancellationToken = default);
}
