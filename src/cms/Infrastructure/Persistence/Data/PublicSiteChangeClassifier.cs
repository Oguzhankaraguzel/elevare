using Application.Abstraction.Services;
using Domain.Entities.Analytics;
using Domain.Entities.Abstractions;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.FormReplyTemplates;
using Domain.Entities.FormSubmissions;
using Domain.Entities.Logs;
using Domain.Entities.Media;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.SiteCodeSnippets;
using Domain.Entities.Sitemaps;
using Domain.Entities.Tags;
using Domain.Entities.UserNotes;
using Domain.Entities.UserReminders;
using Domain.Entities.UserTasks;
using Domain.Entities.Users;
using Domain.Entities.Workflows;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel.Content;

namespace Persistence.Data;

/// <summary>
/// Reads the entries about to be saved and says which cached public pages they
/// make stale — see <see cref="PageCacheTags"/> for what each tag covers.
/// </summary>
/// <remarks>
/// The default is <see cref="PublicSiteChange.MarkEverything"/>: an entity type
/// added later and not listed here clears the whole site, as every change did
/// before this class existed. Narrowing is opt-in, per type, and only where the
/// public site's dependency on it is known.
/// </remarks>
internal static class PublicSiteChangeClassifier
{
    /// <summary>Tables the public site never reads: saving them leaves every page as it was.</summary>
    private static readonly HashSet<Type> CmsOnly =
    [
        typeof(UserNote), typeof(AnnouncementRead), typeof(UserReminder), typeof(UserTask), typeof(UserTaskComment),
        typeof(PageViewHit), typeof(PageClickHit), typeof(AppLog), typeof(AuthEvent),
        typeof(ContentBulkEdit), typeof(ContentBulkEditItem),
        typeof(FormSubmission), typeof(FormSubmissionAttachment), typeof(FormReplyTemplate),
        typeof(WorkflowDefinition), typeof(WorkflowStep), typeof(ApprovalRequest), typeof(ApprovalStepDecision),
        typeof(AppUser), typeof(AppRole), typeof(PasswordSetupToken),
        typeof(IdentityUserRole<Guid>), typeof(IdentityUserClaim<Guid>), typeof(IdentityUserLogin<Guid>),
        typeof(IdentityUserToken<Guid>), typeof(IdentityRoleClaim<Guid>),
    ];

    /// <summary>
    /// Columns that change without changing what a visitor sees: the editor's
    /// unpublished preview copy, and the audit stamps every save writes.
    /// </summary>
    private static readonly HashSet<string> Invisible =
    [
        nameof(PageContent.PreviewGjsHtml), nameof(PageContent.PreviewGjsCss),
        nameof(IAuditable.UpdateDate), nameof(IAuditable.UpdateUserId),
    ];

    /// <summary>Adds what this save changes; counts it only when it writes anything at all.</summary>
    public static void Classify(ChangeTracker changeTracker, PublicSiteChange change)
    {
        bool writes = false;
        foreach (EntityEntry entry in changeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;
            writes = true;
            if (!change.IsEverything)
                ClassifyEntry(changeTracker, entry, change);
        }

        if (writes)
            change.CountSave();
    }

    private static void ClassifyEntry(ChangeTracker changeTracker, EntityEntry entry, PublicSiteChange change)
    {
        if (CmsOnly.Contains(entry.Metadata.ClrType))
            return;

        switch (entry.Entity)
        {
            case PageInfo page:
                PageChanged(entry, page.Id, change);
                return;

            // The page's SEO fields: stored in its row, tracked as their own entry.
            case SeoMeta when OwnerId(entry) is int pageId:
                PageChanged(changeTracker.Entries<PageInfo>().FirstOrDefault(e => e.Entity.Id == pageId), pageId, change);
                return;

            case PageContent content:
                if (OnlyInvisibleColumns(entry))
                    return;
                change.AddTag(PageCacheTags.Page(content.PageInfoId));
                // A listing card falls back to the page's own text when it has no description.
                change.AddTag(PageCacheTags.Listing);
                return;

            case PageTemplate template:
                // A new template is on no page yet: the page that links it is saved too.
                if (entry.State == EntityState.Added || OnlyInvisibleColumns(entry))
                    return;
                change.AddTag(PageCacheTags.Template(template.Id));
                return;

            case PageGroup group:
                change.AddTag(PageCacheTags.Group(group.Id));
                return;

            case Tag:
                change.AddTag(PageCacheTags.Listing);
                return;

            case PageInfoTag pageTag:
                change.AddTag(PageCacheTags.Page(pageTag.PageInfoId));
                change.AddTag(PageCacheTags.Listing);
                return;

            case PageInfoSiteCodeExclusion exclusion:
                change.AddTag(PageCacheTags.Page(exclusion.PageInfoId));
                return;

            case MediaFile:
                // An upload is on no page until a page saved with it.
                if (entry.State != EntityState.Added)
                    change.AddTag(PageCacheTags.Media);
                return;

            case SitemapCache:
                change.AddTag(PageCacheTags.Sitemap);
                return;

            default:
                change.MarkEverything();
                return;
        }
    }

    /// <summary>
    /// A page's title, address, status or place in the tree: the page itself, the
    /// pages naming it (breadcrumbs, language switchers of its translations), every
    /// list it may appear in, and a 404 cached for the address it may now have.
    /// </summary>
    private static void PageChanged(EntityEntry? entry, int pageId, PublicSiteChange change)
    {
        // An added page has no id yet, and nothing can name it before this save.
        if (entry is null || entry.State != EntityState.Added)
        {
            change.AddTag(PageCacheTags.Page(pageId));
            change.AddTag(PageCacheTags.PageInfo(pageId));
        }

        // Without the page's own entry (only its SEO part changed) the group is not
        // known — and the language switchers naming the page do not show SEO fields.
        if (entry is not null)
        {
            PropertyEntry group = entry.Property(nameof(PageInfo.PageGroupId));
            if (entry.State != EntityState.Added && group.OriginalValue is int before)
                change.AddTag(PageCacheTags.Group(before));
            if (group.CurrentValue is int after)
                change.AddTag(PageCacheTags.Group(after));
        }

        change.AddTag(PageCacheTags.Listing);
        change.AddTag(PageCacheTags.NotFound);
    }

    private static int? OwnerId(EntityEntry entry)
    {
        IForeignKey? ownership = entry.Metadata.FindOwnership();
        if (ownership is null || ownership.PrincipalEntityType.ClrType != typeof(PageInfo) || ownership.Properties.Count != 1)
            return null;
        return entry.Property(ownership.Properties[0].Name).CurrentValue as int?;
    }

    private static bool OnlyInvisibleColumns(EntityEntry entry) =>
        entry.State == EntityState.Modified
        && entry.Properties.Where(p => p.IsModified).All(p => Invisible.Contains(p.Metadata.Name));
}
