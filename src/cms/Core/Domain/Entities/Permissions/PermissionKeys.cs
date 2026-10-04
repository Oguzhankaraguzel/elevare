namespace Domain.Entities.Permissions;

/// <summary>
/// Fine-grained permission key constants, assigned to roles as ASP.NET Identity
/// role claims (type <see cref="ClaimType"/>) via <c>RoleManager.AddClaimAsync</c>.
/// Unlike <see cref="Domain.Entities.Users.Roles"/> (fixed role names), a role's
/// permission set is fully editable by a SuperAdmin at runtime through /admin/roles —
/// these constants exist only so C# call sites don't hardcode raw strings.
/// Keep in sync with the default seeding in <c>DatabaseSeeder</c>.
/// </summary>
public static class PermissionKeys
{
    /// <summary>The claim type every permission is stored under.</summary>
    public const string ClaimType = "permission";

    public const string PagesCreate = "Pages.Create";
    public const string PagesEdit = "Pages.Edit";
    public const string PagesDelete = "Pages.Delete";
    public const string PagesPublish = "Pages.Publish";

    public const string TemplatesCreate = "Templates.Create";
    public const string TemplatesEdit = "Templates.Edit";
    public const string TemplatesDelete = "Templates.Delete";

    public const string MediaUpload = "Media.Upload";
    public const string MediaDelete = "Media.Delete";

    public const string LanguagesManage = "Languages.Manage";
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string SiteSettingsManage = "SiteSettings.Manage";
    public const string RedirectsManage = "Redirects.Manage";

    /// <summary>
    /// Reading and rotating outbound-integration credentials (SMTP, CAPTCHA secret,
    /// S3/CDN access keys) — see <c>IntegrationSecret</c>. Deliberately separate from
    /// <see cref="SiteSettingsManage"/> and defaulted to SuperAdmin only (like
    /// <see cref="RolesManage"/>): these are credentials for systems outside the CMS
    /// itself, not day-to-day site configuration.
    /// </summary>
    public const string SecretsManage = "Secrets.Manage";

    /// <summary>
    /// Editing meta defaults, robots.txt and llms.txt. Deliberately separate from
    /// <see cref="SiteSettingsManage"/>: tuning how the site is indexed is content
    /// work an editor should be able to do, whereas Site Settings also holds the
    /// maintenance-mode switch and the mail credentials.
    /// </summary>
    public const string SeoManage = "Seo.Manage";

    public const string FormsViewSubmissions = "Forms.ViewSubmissions";
    public const string FormsManageActions = "Forms.ManageActions";

    public const string BulkEditApply = "BulkEdit.Apply";
    public const string BulkEditRevert = "BulkEdit.Revert";

    public const string TrashView = "Trash.View";
    public const string TrashRestore = "Trash.Restore";

    public const string CustomCodeAuthor = "CustomCode.Author";

    public const string WorkflowsManage = "Workflows.Manage";
    public const string ApprovalsDecide = "Approvals.Decide";

    public const string HangfireAccess = "Hangfire.Access";
    public const string BackupCreate = "Backup.Create";
    public const string CacheManage = "Cache.Manage";
    public const string LogsView = "Logs.View";

    /// <summary>
    /// Viewing who logged in/out and when a session went stale. Kept separate from
    /// <see cref="LogsView"/> (app/error telemetry) on purpose — this is a security
    /// audit trail, not diagnostics, and defaults to SuperAdmin only (see
    /// <c>DatabaseSeeder.DefaultRolePermissions</c>'s Admin exclusion list).
    /// </summary>
    public const string AuthLogsView = "AuthLogs.View";

    /// <summary>All keys above, for UI rendering (grouped checkbox matrix) and seeding.</summary>
    public static readonly string[] All =
    [
        PagesCreate, PagesEdit, PagesDelete, PagesPublish,
        TemplatesCreate, TemplatesEdit, TemplatesDelete,
        MediaUpload, MediaDelete,
        LanguagesManage, UsersManage, RolesManage, SiteSettingsManage, RedirectsManage, SecretsManage, SeoManage,
        FormsViewSubmissions, FormsManageActions,
        BulkEditApply, BulkEditRevert,
        TrashView, TrashRestore,
        CustomCodeAuthor,
        WorkflowsManage, ApprovalsDecide,
        HangfireAccess, BackupCreate, CacheManage, LogsView, AuthLogsView,
    ];
}
