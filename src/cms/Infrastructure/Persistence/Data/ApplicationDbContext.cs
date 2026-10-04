using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Abstractions;
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
using Domain.Entities.SiteCodeSnippets;
using Domain.Entities.Sitemaps;
using Domain.Entities.SiteSettings;
using Domain.Entities.Tags;
using Domain.Entities.UserNotes;
using Domain.Entities.UserReminders;
using Domain.Entities.UserTasks;
using Domain.Entities.Users;
using Domain.Entities.Workflows;
using Domain.Marker;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace Persistence.Data;

internal sealed class ApplicationDbContext
    : IdentityDbContext<AppUser, AppRole, Guid>, ICmsApplicationDbContext
{
    private readonly IUserContext _userContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IUserContext userContext) : base(options)
    {
        _userContext = userContext;
    }

    // ── Content tables ─────────────────────────────────────────────────────────
    public DbSet<PageInfo> PageInfos { get; set; }
    public DbSet<PageContent> PageContents { get; set; }
    public DbSet<PageTemplate> PageTemplates { get; set; }
    public DbSet<PageGroup> PageGroups { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<MediaFile> MediaFiles { get; set; }
    public DbSet<SiteSetting> SiteSettings { get; set; }
    public DbSet<IntegrationSecret> IntegrationSecrets { get; set; }
    public DbSet<SitemapCache> SitemapCaches { get; set; }
    public DbSet<UserNote> UserNotes { get; set; }
    public DbSet<AnnouncementRead> AnnouncementReads { get; set; }
    public DbSet<UserReminder> UserReminders { get; set; }
    public DbSet<UserTask> UserTasks { get; set; }
    public DbSet<UserTaskComment> UserTaskComments { get; set; }
    public DbSet<PageViewHit> PageViewHits { get; set; }
    public DbSet<PageClickHit> PageClickHits { get; set; }
    public DbSet<AppLog> AppLogs { get; set; }
    public DbSet<ContentBulkEdit> ContentBulkEdits { get; set; }
    public DbSet<ContentBulkEditItem> ContentBulkEditItems { get; set; }
    public DbSet<FormSubmission> FormSubmissions { get; set; }
    public DbSet<FormSubmissionAttachment> FormSubmissionAttachments { get; set; }
    public DbSet<FormReplyTemplate> FormReplyTemplates { get; set; }
    public DbSet<Redirect> Redirects { get; set; }
    public DbSet<SiteCodeSnippet> SiteCodeSnippets { get; set; }
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
    public DbSet<ApprovalStepDecision> ApprovalStepDecisions { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<PageInfoTag> PageInfoTags { get; set; }
    public DbSet<PageInfoSiteCodeExclusion> PageInfoSiteCodeExclusions { get; set; }
    public DbSet<AuthEvent> AuthEvents { get; set; }
    public DbSet<PasswordSetupToken> PasswordSetupTokens { get; set; }

    // ── Identity tables (exposed under the interface contract) ──────────────────
    // IdentityDbContext already tracks AppUser/AppRole under Users/Roles.
    // The interface properties delegate to those sets to avoid double entity registration.
    DbSet<AppUser> ICmsApplicationDbContext.AppUsers { get => Users; set => Users = value; }
    DbSet<AppRole> ICmsApplicationDbContext.AppRoles { get => Roles; set => Roles = value; }

    // ── EF Core model configuration ─────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // applies Identity schema
        builder.ApplyConfigurationsFromAssembly(typeof(ICmsDomain).Assembly);
    }

    // ── IApplicationDbContext – async ────────────────────────────────────────────
    // SaveChangesAsync(CancellationToken) is satisfied by DbContext.SaveChangesAsync.

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AuditEntities();

        if (Database.CurrentTransaction is not null)
            return await base.SaveChangesAsync(cancellationToken);

        await using IDbContextTransaction transaction =
            await Database.BeginTransactionAsync(cancellationToken);
        try
        {
            int result = await base.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public override int SaveChanges()
    {
        AuditEntities();
        return SaveChangesCore();
    }


    public async Task<int> SaveChangesWithRetryAsync(int retryCount = 3, CancellationToken cancellationToken = default)
    {
        AuditEntities();

        DbUpdateException? lastException = null;
        for (int attempt = 1; attempt <= retryCount; attempt++)
        {
            await using IDbContextTransaction transaction =
                await Database.BeginTransactionAsync(cancellationToken);
            try
            {
                int result = await base.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                lastException = ex;
                if (attempt < retryCount)
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
        }
        throw lastException!;
    }

    public async Task<bool> EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default)
        => await Database.EnsureCreatedAsync(cancellationToken);

    // ── IApplicationDbContext – sync ─────────────────────────────────────────────
    public int SaveChanges(CancellationToken cancellationToken = default)
    {
        AuditEntities();
        return SaveChangesCore();
    }

    public int SaveChangesWithRetry(int retryCount = 3, CancellationToken cancellationToken = default)
    {
        AuditEntities();

        DbUpdateException? lastException = null;
        for (int attempt = 1; attempt <= retryCount; attempt++)
        {
            using IDbContextTransaction transaction = Database.BeginTransaction();
            try
            {
                int result = base.SaveChanges();
                transaction.Commit();
                return result;
            }
            catch (DbUpdateException ex)
            {
                transaction.Rollback();
                lastException = ex;
                if (attempt < retryCount)
                    Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
        throw lastException!;
    }

    public bool EnsureDatabaseCreated(CancellationToken cancellationToken = default)
        => Database.EnsureCreated();

    // ── Audit ─────────────────────────────────────────────────────────────────
    private int SaveChangesCore()
    {
        if (Database.CurrentTransaction is not null)
            return base.SaveChanges();

        using IDbContextTransaction transaction = Database.BeginTransaction();
        try
        {
            int result = base.SaveChanges();
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void AuditEntities()
    {
        Guid? userId = null;
        try { userId = _userContext.UserId; }
        catch (ApplicationException) { /* user context unavailable — use fallback system user */ }

        DateTime now = DateTime.UtcNow;

        AuditTimestamps(userId, now);
        ApplySoftDeletes(userId, now);

        // The opt-in lasts exactly one save. Without this, an entity purged and then
        // re-attached later in the same context would skip the soft delete again.
        _hardDeletes.Clear();
    }

    /// <summary>Stamps create/update timestamps and user ids on every <see cref="IAuditable"/> entry.</summary>
    private void AuditTimestamps(Guid? userId, DateTime now)
    {
        // Fallback system user (seeded by DatabaseSeeder). Used for CreateUserId
        // which is non-nullable and therefore cannot be left empty.
        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        foreach (EntityEntry<IAuditable> entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreateDate)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.CreateUserId)).CurrentValue = userId ?? systemUserId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdateDate = now;
                    if (userId.HasValue)
                        entry.Entity.UpdateUserId = userId.Value;
                    break;
            }
        }
    }

    /// <summary>
    /// Entities the caller has explicitly asked to destroy. Reference identity, not
    /// keys: the set only has to survive until the save that follows, and comparing
    /// by reference cannot mistake one row for another mid-transaction.
    /// </summary>
    private readonly HashSet<object> _hardDeletes = new(ReferenceEqualityComparer.Instance);

    /// <inheritdoc />
    public void RemovePermanently<TEntity>(TEntity entity) where TEntity : class
    {
        _hardDeletes.Add(entity);
        Set<TEntity>().Remove(entity);
    }

    /// <summary>Converts a real delete into a soft delete on every <see cref="ISoftDeletable"/> entry.</summary>
    private void ApplySoftDeletes(Guid? userId, DateTime now)
    {
        foreach (EntityEntry<ISoftDeletable> entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            // Asked for permanently: leave the entry as Deleted so it really goes.
            if (_hardDeletes.Contains(entry.Entity))
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeleteDate = now;
            if (userId.HasValue)
                entry.Entity.DeleteUserId = userId.Value;

            // IsActive isn't part of ISoftDeletable (see BaseEntity) — every entry here
            // is still a BaseEntity today, so set it directly rather than adding a
            // third interface just for this one flag.
            if (entry.Entity is BaseEntity baseEntity)
                baseEntity.IsActive = false;

            // EF cascade-marks owned dependents (e.g. SeoMeta, stored via table
            // splitting) as Deleted. On a *soft* delete that would null out their
            // non-nullable columns in the UPDATE. Keep their current values.
            foreach (ReferenceEntry reference in entry.References)
            {
                if (reference.TargetEntry is { State: EntityState.Deleted } target
                    && target.Metadata.IsOwned())
                {
                    target.State = EntityState.Unchanged;
                }
            }
        }
    }
}
