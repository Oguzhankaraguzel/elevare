using Application.Features.Commands.Pages.Shared;
﻿using System.Reflection;
using System.Security.Claims;
using Application.Abstraction.Services.Files;
using Domain.Entities.FormReplyTemplates;
using Domain.Entities.IntegrationSecrets;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.Permissions;
using Domain.Entities.SiteCodeSnippets;
using Domain.Entities.SiteSettings;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Persistence.Data;
using SharedKernel.Concrete;
using SharedKernel.Content;

namespace Persistence.Seed;

/// <summary>
/// Idempotent database seeder.
/// Call <see cref="SeedAsync"/> once at application startup — safe to run on every deploy.
/// </summary>
public static class DatabaseSeeder
{
    // ── Fixed GUIDs keep seeded identities stable across environments ─────────
    private static readonly Guid SuperAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid EditorId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Applies pending migrations, then seeds roles, users and languages.
    /// Uses a dedicated DI scope so it is safe to call before the HTTP pipeline starts.
    /// </summary>
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope(); 
        IServiceProvider sp = scope.ServiceProvider;

        ApplicationDbContext context = sp.GetRequiredService<ApplicationDbContext>();
        RoleManager<AppRole> roleManager = sp.GetRequiredService<RoleManager<AppRole>>();
        UserManager<AppUser> userManager = sp.GetRequiredService<UserManager<AppUser>>();
        ILogger logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));
        SeedOptions seed = sp.GetRequiredService<IOptions<SeedOptions>>().Value;

        await context.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(roleManager, logger);
        await SeedUsersAsync(userManager, seed, logger);
        await SeedLanguagesAsync(context, logger, cancellationToken);
        await SeedSiteSettingsAsync(context, logger, cancellationToken);
        await SeedIntegrationSecretsAsync(context, logger, cancellationToken);
        await SeedSiteCodeSnippetsAsync(context, logger, cancellationToken);
        // Must follow languages: the seeded pages are attached to the default one.
        IFileService fileService = sp.GetRequiredService<IFileService>();
        await SeedSystemPagesAsync(context, fileService, logger, cancellationToken);
        await SeedFormReplyTemplatesAsync(context, logger, cancellationToken);
        await BackfillPublishedAtAsync(context, logger, cancellationToken);
    }

    // ── Publish dates ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gives every live page that has none a <c>PublishedAt</c> — the date its Article
    /// block states, else when it was created, the closest thing on record to when
    /// it went live. Runs on every start and only touches pages still missing one,
    /// so after the first run it is a single empty query. Written with
    /// ExecuteUpdate on purpose: a backfill is not an edit, and going through the
    /// change tracker would stamp every page as "updated just now".
    /// </summary>
    private static async Task BackfillPublishedAtAsync(
        ApplicationDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        var missing = await context.PageInfos
            .IgnoreQueryFilters()
            .Where(p => p.PublishedAt == null && p.PageStatus == PageStatus.Published)
            .Select(p => new { p.Id, p.CreateDate, Html = p.Content != null ? p.Content.GjsHtml : null })
            .ToListAsync(cancellationToken);

        foreach (var page in missing)
        {
            DateTime publishedAt = PagePublication.ArticleDate(page.Html) ?? page.CreateDate;
            await context.PageInfos
                .IgnoreQueryFilters()
                .Where(p => p.Id == page.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.PublishedAt, publishedAt), cancellationToken);
        }

        if (missing.Count > 0)
            logger.LogInformation("Backfilled the publish date of {Count} page(s).", missing.Count);
    }

    // ── Form reply templates ──────────────────────────────────────────────────

    /// <summary>
    /// Three starter answers, seeded once. Without them the reply box opens empty
    /// and the feature reads as "write it yourself" — which is the thing canned
    /// replies exist to avoid. Seeded only when the table is completely empty, so a
    /// site that has curated its own set is never topped up behind its back.
    /// </summary>
    private static async Task SeedFormReplyTemplatesAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await context.FormReplyTemplates.IgnoreQueryFilters().AnyAsync(cancellationToken))
            return;

        Guid seedUserId = await context.Users
            .Where(u => u.UserName == "superadmin")
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (seedUserId == Guid.Empty)
            return;

        (string Name, string Subject, string Body, int Order)[] templates =
        [
            ("Teşekkür / alındı bilgisi", "Mesajınızı aldık",
                "Merhaba {{name}},\n\nMesajınız bize ulaştı. En kısa sürede dönüş yapacağız.\n\nİyi günler dileriz.", 1),
            ("Teklif gönderildi", "Teklifiniz hazır",
                "Merhaba {{name}},\n\nTalebiniz doğrultusunda hazırladığımız teklifi ekte paylaşıyoruz. Sorularınız olursa bu e-postayı yanıtlamanız yeterli.\n\nİyi çalışmalar.", 2),
            ("İlgi alanımız dışında", "Talebiniz hakkında",
                "Merhaba {{name}},\n\nİlginiz için teşekkür ederiz. Ne yazık ki bu konu hizmet verdiğimiz alanların dışında kalıyor.\n\nBaşarılar dileriz.", 3),
        ];

        foreach ((string name, string subject, string body, int order) in templates)
        {
            context.FormReplyTemplates.Add(new FormReplyTemplate
            {
                Name = name,
                Subject = subject,
                Body = body,
                SortOrder = order,
                CreateUserId = seedUserId,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} form reply templates.", templates.Length);
    }

    /// <summary>
    /// Seeds the public site's base CSS as ordinary, editable Site Codes rows instead
    /// of leaving it baked into <c>_Layout.cshtml</c> and a separate <c>site.css</c>
    /// file — an operator with no access to the repo could never touch either. Each
    /// piece keeps its own name and can be disabled or reordered independently; the
    /// three together produce byte-for-byte what the layout used to hardcode.
    /// </summary>
    private static async Task SeedSiteCodeSnippetsAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await context.SiteCodeSnippets.IgnoreQueryFilters().AnyAsync(cancellationToken))
            return;

        Guid seedUserId = await context.Users
            .Where(u => u.UserName == "superadmin")
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (seedUserId == Guid.Empty)
            return;

        (string Name, string Content, SiteCodeKind Kind, int Order)[] snippets =
        [
            ("Marka Renkleri (CSS Değişkenleri)",
                """
                <style>
                :root {
                  --elevare-bg: #ffffff;
                  --elevare-surface: #f5f5f5;
                  --elevare-surface-2: #eeeeee;
                  --elevare-text: #111111;
                  --elevare-text-2: #333333;
                  --elevare-muted: #666666;
                  --elevare-border: #dddddd;
                  --elevare-primary: #000000;
                  --elevare-on-primary: #ffffff;
                  --elevare-secondary: #404040;
                  --elevare-heading: #111111;
                  --elevare-link: #000000;
                  --elevare-code-bg: #1a1a1a;
                }
                [data-theme="dark"] {
                  --elevare-bg: #0d0d0d;
                  --elevare-surface: #1a1a1a;
                  --elevare-surface-2: #242424;
                  --elevare-text: #f5f5f5;
                  --elevare-text-2: #cccccc;
                  --elevare-muted: #999999;
                  --elevare-border: #333333;
                  --elevare-primary: #ffffff;
                  --elevare-on-primary: #111111;
                  --elevare-secondary: #bfbfbf;
                  --elevare-heading: #f5f5f5;
                  --elevare-link: #ffffff;
                  --elevare-code-bg: #0a0a0a;
                }
                /* Theme switching, in one rule. This used to say `body`, which is why
                   flipping the theme faded the page background while every menu, card
                   and panel on top of it snapped instantly: the palette variables all
                   change in the same frame, so anything reading one changes at once
                   unless it has a transition of its own.

                   Colour properties only, never `all` — `all` would also animate
                   layout, size and transform, so every hover and every open menu on
                   the site would drag. Armed permanently rather than only while
                   switching, because a transition has to already be in effect BEFORE
                   the value changes; arming it at the moment of the switch is too
                   late and the change still jumps. */
                *, *::before, *::after {
                  transition: background-color .15s ease, color .15s ease,
                              border-color .15s ease, fill .15s ease, stroke .15s ease;
                }
                @media (prefers-reduced-motion: reduce) {
                  /* The near-zero duration rather than `none` is deliberate: it stops
                     the motion while still letting animationend/transitionend fire, so
                     anything that waits on those events does not hang. */
                  *, *::before, *::after {
                    transition: none;
                    animation-duration: .01ms !important;
                    animation-iteration-count: 1 !important;
                  }
                }

                /* Named animations offered by the page builder's Extra panel
                   (animation-name). They live here, in the site's own CSS, rather than
                   being written into each page: a page only ever carries the CSS it was
                   saved with, so keyframes added by the editor would be missing from
                   every page saved before — and present but unused in every page after. */
                @keyframes elevare-pulse {
                  0%, 100% { transform: scale(1); }
                  50%      { transform: scale(1.05); }
                }
                @keyframes elevare-float {
                  0%, 100% { transform: translateY(0); }
                  50%      { transform: translateY(-8px); }
                }
                @keyframes elevare-spin {
                  to { transform: rotate(360deg); }
                }
                </style>
                """, SiteCodeKind.Style, 0),
            // Sets data-theme on <html> BEFORE the body paints — must run after the
            // variables above are defined but before Critical CSS below reads them,
            // or the very first frame renders in the wrong theme (a flash the user
            // would see on every load, not just the first). The Theme Switch block
            // (elevare-blocks.js) and this script share the same localStorage key and
            // attribute, so either one changing it is picked up by the other.
            ("Tema Başlatma (Dark/Light, flash önleme)",
                """
                <script>(function(){try{var s=localStorage.getItem('elevare-theme');var t=s||(window.matchMedia&&window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light');document.documentElement.setAttribute('data-theme',t);}catch(e){}})();</script>
                """, SiteCodeKind.Script, 1),
            ("Kritik CSS",
                """
                <style>
                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                html { font-size: 100%; line-height: 1.5; -webkit-text-size-adjust: 100%; }
                body { font-family: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif; color: var(--elevare-text, #111111); background: var(--elevare-bg, #ffffff); }
                a { color: var(--elevare-link, #000000); text-decoration: none; }
                a:hover { text-decoration: underline; }
                img { max-width: 100%; height: auto; display: block; }
                :focus-visible { outline: 2px solid var(--elevare-primary, #000000); outline-offset: 2px; }
                /* Parked by its OWN height, not by a guessed offset. top:-40px left
                   8px of it on screen: padding and border make the link 48px tall, so
                   -40 put its bottom edge at +8. translateY(-100%) cannot be wrong
                   about that, whatever the font or padding later become. */
                .skip-link { position: absolute; top: 0; left: 8px; transform: translateY(-100%); z-index: 10000; padding: 10px 16px; background: var(--elevare-bg, #fff); color: var(--elevare-text, #111111); border: 2px solid var(--elevare-primary, #000); border-radius: 6px; font-weight: 600; text-decoration: none; transition: transform .15s ease; }
                .skip-link:focus { transform: translateY(8px); }
                .site-main { width: 100%; }
                .error-page { text-align: center; padding: 4rem 1rem; }
                .error-page h1 { font-size: 4rem; font-weight: 700; margin-bottom: 0.5rem; }
                .error-page p { font-size: 1.125rem; color: var(--elevare-muted, #666666); margin-bottom: 1.5rem; }
                .error-page a { display: inline-block; padding: 0.5rem 1.5rem; border: 1px solid var(--elevare-link, #000000); border-radius: 4px; }
                </style>
                """, SiteCodeKind.Style, 2),
            ("Temel Stiller",
                """
                <style>
                html { font-size: 14px; position: relative; min-height: 100%; }
                @media (min-width: 768px) {
                  html { font-size: 16px; }
                }
                @media (max-width: 767px) {
                  .elevare-page-listing .elevare-listing-items {
                    grid-template-columns: 1fr !important;
                    column-count: 1 !important;
                  }
                  .elevare-page-listing .elevare-listing-search {
                    flex-direction: column !important;
                  }
                  .elevare-page-listing .elevare-listing-items a {
                    flex-direction: column !important;
                  }
                  .elevare-page-listing .elevare-listing-items img {
                    width: 100% !important;
                    height: auto !important;
                  }
                }
                </style>
                """, SiteCodeKind.Style, 3),
        ];

        foreach ((string name, string content, SiteCodeKind kind, int order) in snippets)
        {
            context.SiteCodeSnippets.Add(new SiteCodeSnippet
            {
                Name = name,
                Preset = SiteCodePreset.Custom,
                Placement = SiteCodePlacement.HeadStart,
                Kind = kind,
                Content = content,
                SortOrder = order,
                CreateUserId = seedUserId,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} site code snippets.", snippets.Length);
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    // Default permission sets for the 6 fixed roles, matching their behavior before
    // permissions existed (see Roles.cs doc comments). Applied at role creation time,
    // and backfilled once for a pre-existing role that has no permission claims yet
    // (upgrading a database from before the RBAC feature existed) — but never
    // re-applied once a role has any permission claim, so a SuperAdmin's later
    // customization via /admin/roles is never overwritten by a seeder re-run.
    private static readonly Dictionary<string, string[]> DefaultRolePermissions = new()
    {
        [Roles.SuperAdmin] = PermissionKeys.All,
        [Roles.Admin] =
        [
            // AuthLogsView stays out too — a security audit trail of who signed in/out
            // is a SuperAdmin-only concern by default, same reasoning as RolesManage.
            // SecretsManage stays out for the same reason: credentials for systems
            // outside the CMS (SMTP, CAPTCHA, S3/CDN) are an infrastructure-level
            // concern, not day-to-day site administration.
            .. PermissionKeys.All.Except([PermissionKeys.RolesManage, PermissionKeys.HangfireAccess, PermissionKeys.AuthLogsView, PermissionKeys.SecretsManage]),
        ],
        [Roles.Developer] = [PermissionKeys.CustomCodeAuthor, PermissionKeys.PagesEdit, PermissionKeys.TemplatesEdit],
        [Roles.Editor] =
        [
            PermissionKeys.PagesCreate, PermissionKeys.PagesEdit, PermissionKeys.PagesDelete, PermissionKeys.PagesPublish,
            PermissionKeys.TemplatesCreate, PermissionKeys.TemplatesEdit, PermissionKeys.TemplatesDelete,
            PermissionKeys.MediaUpload, PermissionKeys.MediaDelete,
            PermissionKeys.LanguagesManage, PermissionKeys.RedirectsManage, PermissionKeys.SeoManage,
            PermissionKeys.FormsViewSubmissions, PermissionKeys.FormsManageActions,
            PermissionKeys.BulkEditApply, PermissionKeys.BulkEditRevert,
            PermissionKeys.TrashView, PermissionKeys.TrashRestore,
            PermissionKeys.ApprovalsDecide,
        ],
        [Roles.Author] = [PermissionKeys.PagesCreate, PermissionKeys.PagesEdit, PermissionKeys.MediaUpload],
        [Roles.Viewer] = [],
    };

    private static async Task SeedRolesAsync(
        RoleManager<AppRole> roleManager,
        ILogger logger)
    {
        string[] roles = [Roles.SuperAdmin, Roles.Admin, Roles.Developer, Roles.Editor, Roles.Author, Roles.Viewer];

        foreach (string roleName in roles)
        {
            AppRole? role = await roleManager.FindByNameAsync(roleName);

            if (role is null)
            {
                role = new AppRole { Name = roleName };
                IdentityResult result = await roleManager.CreateAsync(role);

                if (!result.Succeeded)
                {
                    logger.LogWarning("[Seed] Could not create role '{Role}': {Errors}",
                        roleName, FormatErrors(result));
                    continue;
                }

                logger.LogInformation("[Seed] Role '{Role}' created.", roleName);
            }
            else
            {
                // Role predates the RBAC feature (or a SuperAdmin has already customized its
                // permissions since) — only backfill if it has no permission claims at all yet.
                IList<Claim> existingClaims = await roleManager.GetClaimsAsync(role);
                if (existingClaims.Any(c => c.Type == PermissionKeys.ClaimType))
                {
                    await TopUpSuperAdminAsync(roleManager, role, existingClaims, logger);
                    continue;
                }
            }

            foreach (string permission in DefaultRolePermissions.GetValueOrDefault(roleName, []))
                await roleManager.AddClaimAsync(role, new Claim(PermissionKeys.ClaimType, permission));
        }
    }

    /// <summary>
    /// Grants SuperAdmin any permission key added since the database was created.
    /// <para>
    /// Every other role is left alone on purpose — its permission set is a decision a
    /// SuperAdmin made through /admin/roles, and a deployment should not quietly widen
    /// it. SuperAdmin is the exception because a key nobody holds is a feature nobody
    /// can reach, including the person whose job is handing it out.
    /// </para>
    /// </summary>
    private static async Task TopUpSuperAdminAsync(
        RoleManager<AppRole> roleManager,
        AppRole role,
        IList<Claim> existingClaims,
        ILogger logger)
    {
        if (role.Name != Roles.SuperAdmin)
            return;

        HashSet<string> held = [.. existingClaims
            .Where(c => c.Type == PermissionKeys.ClaimType)
            .Select(c => c.Value)];

        string[] missing = [.. PermissionKeys.All.Where(key => !held.Contains(key))];

        if (missing.Length == 0)
            return;

        foreach (string permission in missing)
            await roleManager.AddClaimAsync(role, new Claim(PermissionKeys.ClaimType, permission));

        logger.LogInformation(
            "[Seed] {Count} new permission(s) granted to SuperAdmin: {Permissions}. "
            + "Assign them to other roles from /admin/roles.",
            missing.Length, string.Join(", ", missing));
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the configured accounts. Nothing is invented here: a password that
    /// ships in source is public the moment the repository is, so an unconfigured
    /// account is skipped with a loud warning rather than given a default.
    /// </summary>
    private static async Task SeedUsersAsync(
        UserManager<AppUser> userManager,
        SeedOptions seed,
        ILogger logger)
    {
        if (seed.SuperAdmin.IsConfigured)
        {
            await EnsureUserAsync(userManager, logger,
                id: SuperAdminId,
                userName: seed.SuperAdmin.UserName,
                email: seed.SuperAdmin.Email,
                password: seed.SuperAdmin.Password,
                role: Roles.SuperAdmin);
        }
        else if (!await userManager.Users.AnyAsync())
        {
            // An empty database and no configured administrator means nobody can
            // sign in at all — say so plainly instead of letting the operator find
            // out at the login screen.
            logger.LogError(
                "[Seed] No SuperAdmin configured and no users exist. Set Seed:SuperAdmin:UserName, "
                + "Seed:SuperAdmin:Email and Seed:SuperAdmin:Password (or the Seed__SuperAdmin__* "
                + "environment variables) — otherwise there is no way to log in.");
        }

        if (seed.Editor?.IsConfigured == true)
        {
            await EnsureUserAsync(userManager, logger,
                id: EditorId,
                userName: seed.Editor.UserName,
                email: seed.Editor.Email,
                password: seed.Editor.Password,
                role: Roles.Editor);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<AppUser> userManager,
        ILogger logger,
        Guid id,
        string userName,
        string email,
        string password,
        string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        AppUser user = new()
        {
            Id = id,
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        IdentityResult result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("[Seed] User '{Email}' created → role '{Role}'.", email, role);
        }
        else
        {
            logger.LogWarning("[Seed] Could not create user '{Email}': {Errors}",
                email, FormatErrors(result));
        }
    }

    // ── Languages ─────────────────────────────────────────────────────────────

    // internal rather than private so a test can run it against the real model and
    // assert what a brand-new install actually gets — see FreshInstallSeedTests.
    internal static async Task SeedLanguagesAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await context.Languages.AnyAsync(cancellationToken))
            return;

        // CreateUserId is set explicitly here; AuditEntities() will skip it
        // because IUserContext is unavailable outside an HTTP request scope.
        //
        // Both are seeded published. IsPublished exists to hold a language back while
        // a translator fills it in, which is a decision about languages someone adds
        // later — a starter language that is active but invisible is just a trap:
        // the admin writes a home page, publishes it, and the site still shows nothing.
        Language[] languages =
        [
            new()
            {
                CreateUserId    = SuperAdminId,
                NameInNative    = "Türkçe",
                NameInEnglish   = "Turkish",
                TwoLetterCode   = "tr",
                IsDefault       = true,
                DisplayOrder    = 1,
                IsRtl           = false,
                IsActive        = true,
                IsPublished     = true,
            },
            new()
            {
                CreateUserId    = SuperAdminId,
                NameInNative    = "English",
                NameInEnglish   = "English",
                TwoLetterCode   = "en",
                IsDefault       = false,
                DisplayOrder    = 2,
                IsRtl           = false,
                IsActive        = true,
                IsPublished     = true,
            },
        ];

        await context.Languages.AddRangeAsync(languages, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[Seed] {Count} language(s) seeded.", languages.Length);
    }

    // ── System pages ──────────────────────────────────────────────────────────

    /// <summary>
    /// Ships the reserved-slug pages a site needs before anyone has authored
    /// anything — currently the maintenance screen, so a brand-new install can be
    /// closed to visitors and still show something finished rather than a bare
    /// built-in notice.
    /// <para>
    /// Seeded as an ordinary published builder page, so the editor can restyle or
    /// replace it like any other content. Only the DEFAULT language gets a row:
    /// the screen carries no prose beyond the brand, and
    /// <c>SystemPageProvider</c> already falls back to the default language when a
    /// visitor's language has no page of its own.
    /// </para>
    /// <para>
    /// Idempotent by slug, so re-running never resurrects a page the site owner
    /// deliberately deleted, and never overwrites their edits.
    /// </para>
    /// </summary>
    private static async Task SeedSystemPagesAsync(
        ApplicationDbContext context,
        IFileService fileService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        const string maintenanceSlug = "maintenance";

        Language? defaultLanguage = await context.Languages
            .FirstOrDefaultAsync(l => l.IsDefault && l.IsActive, cancellationToken);

        if (defaultLanguage is null)
        {
            logger.LogWarning("[Seed] No default language; system pages skipped.");
            return;
        }

        // Checked across ALL languages: the slug is reserved site-wide, and a
        // translation existing without the default-language original is a legitimate
        // state we must not trample.
        if (!await context.PageInfos.IgnoreQueryFilters()
                .AnyAsync(p => p.Slug == maintenanceSlug, cancellationToken))
        {
            (string? background, string mark) = await UploadMaintenanceImagesAsync(fileService, logger, cancellationToken);
            var maintenancePage = new PageInfo
            {
                CreateUserId = SuperAdminId,
                Slug = maintenanceSlug,
                FullSlug = maintenanceSlug,
                LanguageId = defaultLanguage.Id,
                PageStatus = PageStatus.Published,
                IsActive = true,
                SeoMeta = new SeoMeta
                {
                    // Admin-facing only — the visitor-facing document title is the brand,
                    // set by MaintenanceModeMiddleware. This is what labels the row in the
                    // CMS page list, so it says what the page IS.
                    Title = "Bakım Modu",
                    MetaDescription = string.Empty,
                    MetaAuthor = string.Empty,
                    IsCanonical = false,
                },
                Content = new PageContent
                {
                    CreateUserId = SuperAdminId,
                    GjsHtml = MaintenancePageContent.HtmlWith(background, mark),
                    GjsCss = MaintenancePageContent.Css,
                    // No project blob: the builder rebuilds its component tree from the
                    // HTML/CSS above on first open, which is exactly what we want for
                    // hand-authored markup.
                    GjsData = null,
                },
            };

            await context.PageInfos.AddAsync(maintenancePage, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("[Seed] Maintenance page seeded ('{Slug}').", maintenanceSlug);
        }
        else
        {
            await RepairMaintenanceImagesAsync(context, fileService, logger, cancellationToken);
        }

        // English is always one of the two languages SeedLanguagesAsync ships, so
        // this is only ever missing if an operator later deactivated/deleted it —
        // in which case seeding just the default-language page below is correct.
        Language? englishLanguage = await context.Languages
            .FirstOrDefaultAsync(l => l.TwoLetterCode == "en" && l.IsActive, cancellationToken);

        await SeedTranslatedSystemPageAsync(
            context, fileService, logger,
            slug: "home", titleFor: lang => lang == "en" ? "Home" : "Ana Sayfa",
            defaultLanguage, englishLanguage,
            resourceFileName: "home-bg.jpg", mediaTitle: "Ana sayfa arka planı",
            htmlFor: HomePageContent.Html, css: HomePageContent.Css,
            // "home" is the one reserved slug PageInfo.ComputeFullSlug strips back out
            // to "" (default language) / the bare language code (others) — see there.
            fullSlugFor: lang => lang.IsDefault ? string.Empty : lang.TwoLetterCode,
            cancellationToken);

        await SeedTranslatedSystemPageAsync(
            context, fileService, logger,
            slug: "404", titleFor: lang => lang == "en" ? "404 - Page Not Found" : "404 - Sayfa Bulunamadı",
            defaultLanguage, englishLanguage,
            resourceFileName: "404-bg.jpg", mediaTitle: "404 sayfası arka planı",
            htmlFor: NotFoundPageContent.Html, css: NotFoundPageContent.Css,
            fullSlugFor: lang => lang.IsDefault ? "404" : $"{lang.TwoLetterCode}/404",
            cancellationToken);

        await SeedTranslatedSystemPageAsync(
            context, fileService, logger,
            slug: "500", titleFor: lang => lang == "en" ? "500 - Server Error" : "500 - Sunucu Hatası",
            defaultLanguage, englishLanguage,
            resourceFileName: "500-bg.jpg", mediaTitle: "500 sayfası arka planı",
            htmlFor: ServerErrorPageContent.Html, css: ServerErrorPageContent.Css,
            fullSlugFor: lang => lang.IsDefault ? "500" : $"{lang.TwoLetterCode}/500",
            cancellationToken);
    }

    /// <summary>
    /// The maintenance screen's photo and mark, put into the Media Library so the
    /// page shows them in the CMS editor as well as on the site. A failed upload
    /// is cosmetic: the photo is left out (the gradient shows) and the mark falls
    /// back to the site's own copy.
    /// </summary>
    private static async Task<(string? Background, string Mark)> UploadMaintenanceImagesAsync(
        IFileService fileService, ILogger logger, CancellationToken cancellationToken)
    {
        Result<string> background = await UploadSeedImageAsync(fileService, "maintenance-bg.jpg", "Bakım sayfası arka planı", cancellationToken);
        Result<string> mark = await UploadSeedImageAsync(fileService, "elevare-mark-white.png", "Bakım sayfası logosu", cancellationToken);
        if (background.IsFailure || mark.IsFailure)
            logger.LogWarning("[Seed] Could not upload the maintenance page images; using the site's own copies.");
        return (background.IsSuccess ? background.Value : null,
                mark.IsSuccess ? mark.Value : MaintenancePageContent.MarkImagePath);
    }

    /// <summary>
    /// Installs seeded before the images moved to the Media Library still point the
    /// maintenance page at <c>/img/…</c>, which only the public site serves — in the
    /// editor the photo vanished and the mark was a broken image. Swaps exactly those
    /// two addresses, so a page the owner restyled keeps everything else; once
    /// swapped there is nothing left to match and this does nothing.
    /// </summary>
    private static async Task RepairMaintenanceImagesAsync(
        ApplicationDbContext context, IFileService fileService, ILogger logger, CancellationToken cancellationToken)
    {
        List<PageContent> stale = await context.PageInfos
            .Where(p => p.Slug == "maintenance" && p.Content != null
                && (p.Content.GjsHtml!.Contains(MaintenancePageContent.BackgroundImagePath)
                    || p.Content.GjsHtml!.Contains(MaintenancePageContent.MarkImagePath)))
            .Select(p => p.Content!)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
            return;

        (string? background, string mark) = await UploadMaintenanceImagesAsync(fileService, logger, cancellationToken);
        if (background is null || mark == MaintenancePageContent.MarkImagePath)
            return;

        static string? Swap(string? value, string background, string mark) => value?
            .Replace(MaintenancePageContent.BackgroundImagePath, background, StringComparison.Ordinal)
            .Replace(MaintenancePageContent.MarkImagePath, mark, StringComparison.Ordinal);

        foreach (PageContent content in stale)
        {
            content.GjsHtml = Swap(content.GjsHtml, background, mark);
            content.GjsData = Swap(content.GjsData, background, mark);
            content.PreviewGjsHtml = Swap(content.PreviewGjsHtml, background, mark);
        }
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("[Seed] Maintenance page images moved to the Media Library.");
    }

    /// <summary>
    /// Seeds one reserved-slug page per given language (default + English, when
    /// active), each with the same background photo (uploaded to the Media Library
    /// exactly once and reused across both rows — one physical file, two pages) but
    /// its own language's copy.
    /// <para>
    /// Idempotent by slug across ALL languages, same as the maintenance page above:
    /// if a page with this slug exists in ANY language, nothing is added or
    /// touched — re-running never resurrects a page the site owner deleted, and an
    /// operator who removed only the English half does not get it silently redone
    /// for them, which would be surprising in the other direction.
    /// </para>
    /// </summary>
    private static async Task SeedTranslatedSystemPageAsync(
        ApplicationDbContext context,
        IFileService fileService,
        ILogger logger,
        string slug,
        Func<string, string> titleFor,
        Language defaultLanguage,
        Language? englishLanguage,
        string resourceFileName,
        string mediaTitle,
        Func<string, string?, string> htmlFor,
        string css,
        Func<Language, string> fullSlugFor,
        CancellationToken cancellationToken)
    {
        if (await context.PageInfos.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug, cancellationToken))
            return;

        Result<string> mediaUrl = await UploadSeedImageAsync(fileService, resourceFileName, mediaTitle, cancellationToken);
        if (mediaUrl.IsFailure)
        {
            // A missing background photo is cosmetic, not fatal — the page still
            // renders (background-color:#0b111d carries it, see *PageContent.Css).
            // Blocking the whole seed run over an image would leave the site with
            // no 404/500/home page at all over something an operator can fix later
            // by just re-uploading the photo onto the already-seeded page.
            logger.LogWarning(
                "[Seed] Could not upload background photo for '{Slug}' ({Error}); seeding without it.",
                slug, mediaUrl.Error.Description);
        }

        // Null (rather than a broken <img src>) is what makes a failed upload
        // "cosmetic, not fatal" — see htmlFor's own doc comment (HomePageContent
        // and its siblings) for how each page renders without one.
        string? imageUrl = mediaUrl.IsSuccess ? mediaUrl.Value : null;

        List<Language> targetLanguages = [defaultLanguage];
        if (englishLanguage is not null)
            targetLanguages.Add(englishLanguage);

        // Without this, each language's row is a page in its own right and the Pages
        // grid — which groups by PageGroup the same way CreatePageTranslation does —
        // shows "404" (TR) and "404" (EN) as two unrelated pages instead of one page
        // with two language variants. Only needed when there is more than one row to
        // link; a lone default-language page (no active English, or "maintenance",
        // which only ever seeds one) has nothing to group with, exactly like an
        // ordinary page nobody has translated yet.
        PageGroup? group = null;
        if (targetLanguages.Count > 1)
        {
            group = new PageGroup { Name = titleFor(defaultLanguage.TwoLetterCode) };
            await context.PageGroups.AddAsync(group, cancellationToken);
        }

        foreach (Language language in targetLanguages)
        {
            var page = new PageInfo
            {
                CreateUserId = SuperAdminId,
                Slug = slug,
                FullSlug = fullSlugFor(language),
                LanguageId = language.Id,
                PageGroup = group,
                PageStatus = PageStatus.Published,
                IsActive = true,
                SeoMeta = new SeoMeta
                {
                    Title = titleFor(language.TwoLetterCode),
                    MetaDescription = string.Empty,
                    MetaAuthor = string.Empty,
                    IsCanonical = false,
                },
                Content = new PageContent
                {
                    CreateUserId = SuperAdminId,
                    GjsHtml = htmlFor(language.TwoLetterCode, imageUrl),
                    GjsCss = css,
                    GjsData = null,
                },
            };

            await context.PageInfos.AddAsync(page, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[Seed] '{Slug}' page seeded for {Count} language(s).", slug, targetLanguages.Count);
    }

    /// <summary>
    /// Reads a seed photo embedded in this assembly (see Persistence.csproj) and
    /// pushes it through the real upload pipeline — the same <see cref="IFileService"/>
    /// path a CMS user's own upload takes — so it ends up as a normal, visible row
    /// in the Media Library rather than a file the CMS itself does not know about.
    /// </summary>
    private static async Task<Result<string>> UploadSeedImageAsync(
        IFileService fileService,
        string resourceFileName,
        string title,
        CancellationToken cancellationToken)
    {
        Assembly assembly = typeof(DatabaseSeeder).Assembly;
        string resourceName = $"Persistence.Seed.Assets.{resourceFileName}";

        await using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream is null)
            return Result.Failure<string>(SeedErrors.ImageMissing(resourceName));

        using var buffer = new MemoryStream();
        await resourceStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        Result<FileResult> uploaded = await fileService.UploadAsync(
            new FileUploadRequest
            {
                FileName = resourceFileName,
                ContentType = resourceFileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg",
                Content = buffer,
                FileSize = buffer.Length,
                Title = title,
                AltText = title,
            },
            cancellationToken);

        return uploaded.IsSuccess
            ? Result.Success(uploaded.Value.Url)
            : Result.Failure<string>(uploaded.Error);
    }

    // ── Site Settings ─────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds one row per known setting key, leaving <see cref="SiteSetting.Value"/>
    /// empty (except sensible defaults) for the CMS user to fill in via the Site
    /// Settings screen. Idempotent: only inserts keys that don't already exist, so
    /// re-running (or adding new keys in a later release) never touches existing values.
    /// </summary>
    private static async Task SeedSiteSettingsAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // DisplayName/Description below are CmsMessages resx KEYS (see
        // Settings_Field_*_Label/_Desc), not literal text — SiteSettingsPage.razor
        // resolves them through the CMS's active-language localizer at render time.
        // Storing keys rather than one fixed-language string is what lets the same
        // seeded row read correctly in both TR and EN; RegroupSiteSettingsAsync below
        // re-syncs existing rows to match, so an upgrade picks this up too, not just a
        // fresh install.
        (string Key, string? Value, string DisplayName, string? Description, SiteSettingGroup Group, string DataType)[] catalog =
        [
            // Identity — brand basics. Global CSS used to live here too; it retired
            // to Site Codes (a "Custom Code" snippet at HeadEnd) since that already
            // has ordering, naming and an on/off switch that a single free-text CSS
            // field never did — see RetiredSettingKeys.
            ("General.SiteName", "Elevare", "Settings_Field_GeneralSiteName_Label", "Settings_Field_GeneralSiteName_Desc", SiteSettingGroup.Identity, "string"),
            ("General.Tagline", null, "Settings_Field_GeneralTagline_Label", "Settings_Field_GeneralTagline_Desc", SiteSettingGroup.Identity, "string"),
            ("Appearance.LogoUrl", null, "Settings_Field_AppearanceLogoUrl_Label", "Settings_Field_AppearanceLogoUrl_Desc", SiteSettingGroup.Identity, "url"),
            ("Appearance.FaviconUrl", null, "Settings_Field_AppearanceFaviconUrl_Label", "Settings_Field_AppearanceFaviconUrl_Desc", SiteSettingGroup.Identity, "url"),
            // Written by the CMS when it generates the sized favicon PNGs (see
            // IFaviconService); "hidden" keeps it off the Site Settings screen.
            ("Appearance.FaviconSetVersion", null, "Settings_Field_AppearanceFaviconSetVersion_Label", null, SiteSettingGroup.Identity, "hidden"),
            ("Advanced.PublicSiteBaseUrl", null, "Settings_Field_AdvancedPublicSiteBaseUrl_Label", "Settings_Field_AdvancedPublicSiteBaseUrl_Desc", SiteSettingGroup.Identity, "url"),

            // SEO
            // Empty, not "Elevare": _Layout.cshtml already falls back to
            // General.SiteName whenever this is unset, and a non-empty seeded
            // default here would shadow that fallback for every fresh install —
            // the operator sets a site name once and the title tag would keep
            // reading "Elevare" regardless, with no obvious reason why.
            ("Seo.DefaultMetaTitleSuffix", null, "Settings_Field_SeoDefaultMetaTitleSuffix_Label", "Settings_Field_SeoDefaultMetaTitleSuffix_Desc", SiteSettingGroup.Seo, "string"),
            ("Seo.DefaultMetaDescription", null, "Settings_Field_SeoDefaultMetaDescription_Label", "Settings_Field_SeoDefaultMetaDescription_Desc", SiteSettingGroup.Seo, "string"),
            ("Seo.DefaultOgImageUrl", null, "Settings_Field_SeoDefaultOgImageUrl_Label", "Settings_Field_SeoDefaultOgImageUrl_Desc", SiteSettingGroup.Seo, "url"),
            // Seo.GoogleSiteVerification lives in Site Codes now — see RetiredSettingKeys.
            ("Seo.RobotsTxt", null, "Settings_Field_SeoRobotsTxt_Label", "Settings_Field_SeoRobotsTxt_Desc", SiteSettingGroup.Seo, "text"),
            ("Seo.LlmsTxt", null, "Settings_Field_SeoLlmsTxt_Label", "Settings_Field_SeoLlmsTxt_Desc", SiteSettingGroup.Seo, "text"),

            // İletişim & Sosyal — where to reach the business and where to find it online.
            ("Social.FacebookUrl", null, "Settings_Field_SocialFacebookUrl_Label", null, SiteSettingGroup.ContactSocial, "url"),
            ("Social.InstagramUrl", null, "Settings_Field_SocialInstagramUrl_Label", null, SiteSettingGroup.ContactSocial, "url"),
            ("Social.XUrl", null, "Settings_Field_SocialXUrl_Label", null, SiteSettingGroup.ContactSocial, "url"),
            ("Social.LinkedInUrl", null, "Settings_Field_SocialLinkedInUrl_Label", null, SiteSettingGroup.ContactSocial, "url"),
            ("Social.YoutubeUrl", null, "Settings_Field_SocialYoutubeUrl_Label", null, SiteSettingGroup.ContactSocial, "url"),
            ("Social.WhatsappNumber", null, "Settings_Field_SocialWhatsappNumber_Label", "Settings_Field_SocialWhatsappNumber_Desc", SiteSettingGroup.ContactSocial, "string"),
            // Read by the "Google Haberler'de Takip Et" block when its own field is empty.
            ("Social.GoogleNewsUrl", null, "Settings_Field_SocialGoogleNewsUrl_Label", "Settings_Field_SocialGoogleNewsUrl_Desc", SiteSettingGroup.ContactSocial, "url"),

            ("Contact.Phone", null, "Settings_Field_ContactPhone_Label", null, SiteSettingGroup.ContactSocial, "string"),
            ("Contact.Email", null, "Settings_Field_ContactEmail_Label", null, SiteSettingGroup.ContactSocial, "string"),
            ("Contact.Address", null, "Settings_Field_ContactAddress_Label", "Settings_Field_ContactAddress_Desc", SiteSettingGroup.ContactSocial, "string"),
            // Split out because structured data needs them as separate fields — a
            // PostalAddress with one free-text line is not usable for local search.
            ("Contact.AddressLocality", null, "Settings_Field_ContactAddressLocality_Label", "Settings_Field_ContactAddressLocality_Desc", SiteSettingGroup.ContactSocial, "string"),
            ("Contact.AddressRegion", null, "Settings_Field_ContactAddressRegion_Label", "Settings_Field_ContactAddressRegion_Desc", SiteSettingGroup.ContactSocial, "string"),
            ("Contact.PostalCode", null, "Settings_Field_ContactPostalCode_Label", null, SiteSettingGroup.ContactSocial, "string"),
            ("Contact.AddressCountry", null, "Settings_Field_ContactAddressCountry_Label", "Settings_Field_ContactAddressCountry_Desc", SiteSettingGroup.ContactSocial, "string"),
            ("Contact.WorkingHours", null, "Settings_Field_ContactWorkingHours_Label", "Settings_Field_ContactWorkingHours_Desc", SiteSettingGroup.ContactSocial, "string"),
            ("Contact.MapEmbedUrl", null, "Settings_Field_ContactMapEmbedUrl_Label", "Settings_Field_ContactMapEmbedUrl_Desc", SiteSettingGroup.ContactSocial, "url"),
            // Read by the "Google'da Yorum Yaz" block when its own field is empty.
            ("Contact.GooglePlaceId", null, "Settings_Field_ContactGooglePlaceId_Label", "Settings_Field_ContactGooglePlaceId_Desc", SiteSettingGroup.ContactSocial, "string"),

            // Analytics tags used to live here as one setting per script. They are now
            // rows in SiteCodeSnippets (Site Codes screen): a setting can hold exactly
            // one snippet, cannot be named or switched off, has no order relative to
            // its neighbours, and was written to the page unchecked. See RetiredKeys.

            // Sistem — only the non-secret "From" identity lives here; SMTP host/port/
            // credentials live in the encrypted IntegrationSecrets table instead (the
            // "Sırlar" screen — see SeedIntegrationSecretsAsync) since this table has
            // no encryption-at-rest story and never will need one for this pair.
            ("Email.FromAddress", null, "Settings_Field_EmailFromAddress_Label", "Settings_Field_EmailFromAddress_Desc", SiteSettingGroup.System, "string"),
            ("Email.FromDisplayName", null, "Settings_Field_EmailFromDisplayName_Label", "Settings_Field_EmailFromDisplayName_Desc", SiteSettingGroup.System, "string"),

            // ON for a brand-new install: a site with no content yet should greet
            // visitors with the finished maintenance screen rather than an empty
            // shell. The operator turns it off here once the site is ready. Only
            // ever applied at INSERT — an existing install's value is never touched
            // (see the idempotency note on SeedSiteSettingsAsync), so an upgrade
            // cannot silently close a live site.
            ("Advanced.MaintenanceModeEnabled", "true", "Settings_Field_AdvancedMaintenanceModeEnabled_Label", "Settings_Field_AdvancedMaintenanceModeEnabled_Desc", SiteSettingGroup.System, "bool"),

            // ON by default — the public site always sent the bare domain to its www
            // address, and that stays the behaviour until someone switches it off.
            ("Advanced.RedirectToWww", "true", "Settings_Field_AdvancedRedirectToWww_Label", "Settings_Field_AdvancedRedirectToWww_Desc", SiteSettingGroup.System, "bool"),

            // Prefetching costs a request for links the visitor hovers but never
            // clicks, which is the whole reason it is a switch and not a constant:
            // on metered or very low-traffic hosting that trade is not automatically
            // worth it. View counts are unaffected either way — the counter is a
            // fetch() the prefetched document never runs (see _Layout.cshtml).
            ("Performance.InstantPageEnabled", "true", "Settings_Field_PerformanceInstantPageEnabled_Label", "Settings_Field_PerformanceInstantPageEnabled_Desc", SiteSettingGroup.System, "bool"),

            // Integrations — only non-secret, safe-to-expose values live here (mirrors
            // Email.FromAddress's precedent above); actual credentials — S3 keys, the
            // Captcha secret key — live in the encrypted IntegrationSecrets table
            // instead; see ObjectStorageOptions/CaptchaOptions and
            // SeedIntegrationSecretsAsync.
            // Pull-CDN only, and deliberately so — see LocalDiskBlobStorage. Push-style
            // object storage is a different mechanism entirely (files are uploaded to
            // the provider, credentials required), which is why its config lives in
            // IntegrationSecrets under ObjectStorage rather than here.
            ("Integrations.CdnBaseUrl", null, "Settings_Field_IntegrationsCdnBaseUrl_Label", "Settings_Field_IntegrationsCdnBaseUrl_Desc", SiteSettingGroup.Integrations, "url"),
            ("Integrations.CaptchaProvider", "none", "Settings_Field_IntegrationsCaptchaProvider_Label", "Settings_Field_IntegrationsCaptchaProvider_Desc", SiteSettingGroup.Integrations, "captcha-provider"),
            ("Integrations.CaptchaSiteKey", null, "Settings_Field_IntegrationsCaptchaSiteKey_Label", "Settings_Field_IntegrationsCaptchaSiteKey_Desc", SiteSettingGroup.Integrations, "string"),
            ("Integrations.ErrorWebhookUrl", null, "Settings_Field_IntegrationsErrorWebhookUrl_Label", "Settings_Field_IntegrationsErrorWebhookUrl_Desc", SiteSettingGroup.Integrations, "url"),
        ];

        await RemoveRetiredSettingsAsync(context, logger, cancellationToken);
        await RegroupSiteSettingsAsync(context, catalog, logger, cancellationToken);

        List<string> existingKeys = await context.SiteSettings
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        var toAdd = catalog
            .Where(c => !existingKeys.Contains(c.Key))
            .Select(c => new SiteSetting
            {
                CreateUserId = SuperAdminId,
                Key          = c.Key,
                Value        = c.Value,
                DisplayName  = c.DisplayName,
                Description  = c.Description,
                Group        = c.Group,
                DataType     = c.DataType,
                IsSystem     = true,
            })
            .ToList();

        if (toAdd.Count == 0)
            return;

        await context.SiteSettings.AddRangeAsync(toAdd, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[Seed] {Count} site setting(s) seeded.", toAdd.Count);
    }

    /// <summary>
    /// Brings every existing row's LABELLING — group, display name, description, data
    /// type — back in line with <paramref name="catalog"/>. Site Settings collapsed from
    /// 9 tabs to 4, and a setting's explanatory text changes as the thing it controls
    /// gains a real use; both need to reach a database that seeded its rows under the
    /// old wording, not just a fresh install. The stored <c>Value</c> is never touched:
    /// that is the operator's, not the catalogue's. Idempotent — a row that already
    /// matches costs nothing to re-run.
    /// </summary>
    private static async Task RegroupSiteSettingsAsync(
        ApplicationDbContext context,
        (string Key, string? Value, string DisplayName, string? Description, SiteSettingGroup Group, string DataType)[] catalog,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Dictionary<string, (string DisplayName, string? Description, SiteSettingGroup Group, string DataType)> targetByKey =
            catalog.ToDictionary(c => c.Key, c => (c.DisplayName, c.Description, c.Group, c.DataType));

        List<SiteSetting> settings = await context.SiteSettings
            .Where(s => !s.IsDeleted)
            .ToListAsync(cancellationToken);

        int changed = 0;
        foreach (SiteSetting setting in settings)
        {
            if (!targetByKey.TryGetValue(setting.Key, out (string DisplayName, string? Description, SiteSettingGroup Group, string DataType) target))
                continue;

            if (setting.Group == target.Group
                && setting.DisplayName == target.DisplayName
                && setting.Description == target.Description
                && setting.DataType == target.DataType)
            {
                continue;
            }

            setting.Group = target.Group;
            setting.DisplayName = target.DisplayName;
            setting.Description = target.Description;
            setting.DataType = target.DataType;
            changed++;
        }

        if (changed == 0)
            return;

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("[Seed] {Count} site setting(s) re-synced to the current catalogue.", changed);
    }

    /// <summary>
    /// Seeds the catalogue row for every outbound-integration credential the CMS Admin
    /// screen ("Sırlar" / Secrets, gated on <c>Secrets.Manage</c>) can edit — the fields
    /// that used to live only in <c>appsettings.json</c>. Same idempotency shape as
    /// <see cref="SeedSiteSettingsAsync"/>: only missing keys are inserted, an existing
    /// row's <c>Value</c> is never touched by a re-run.
    /// <para>
    /// <c>Key</c> uses <c>IConfiguration</c>'s own <c>:</c> separator (e.g.
    /// <c>"Email:Password"</c>), not SiteSettings' <c>.</c> convention — these rows are
    /// read back at startup to override the matching config path directly, see
    /// <c>IntegrationSecretsBootstrap</c>.
    /// </para>
    /// </summary>
    private static async Task SeedIntegrationSecretsAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        (string Key, string? Value, string DisplayName, string? Description, IntegrationSecretCategory Category, bool IsSecret, string DataType)[] catalog =
        [
            ("Email:Host", null, "Secrets_Field_EmailHost_Label", "Secrets_Field_EmailHost_Desc", IntegrationSecretCategory.Email, false, "string"),
            ("Email:Port", "587", "Secrets_Field_EmailPort_Label", null, IntegrationSecretCategory.Email, false, "int"),
            ("Email:EnableSsl", "true", "Secrets_Field_EmailEnableSsl_Label", null, IntegrationSecretCategory.Email, false, "bool"),
            ("Email:UserName", null, "Secrets_Field_EmailUserName_Label", null, IntegrationSecretCategory.Email, false, "string"),
            ("Email:Password", null, "Secrets_Field_EmailPassword_Label", "Secrets_Field_EmailPassword_Desc", IntegrationSecretCategory.Email, true, "string"),

            // Pairs with Integrations.CaptchaProvider/CaptchaSiteKey (Site Settings —
            // safe to expose, so they stay there). This is the one value that never was.
            ("Captcha:SecretKey", null, "Secrets_Field_CaptchaSecretKey_Label", "Secrets_Field_CaptchaSecretKey_Desc", IntegrationSecretCategory.Captcha, true, "string"),

            ("ObjectStorage:Provider", "Local", "Secrets_Field_ObjectStorageProvider_Label", "Secrets_Field_ObjectStorageProvider_Desc", IntegrationSecretCategory.ObjectStorage, false, "storage-provider"),
            ("ObjectStorage:BucketName", null, "Secrets_Field_ObjectStorageBucketName_Label", null, IntegrationSecretCategory.ObjectStorage, false, "string"),
            ("ObjectStorage:Region", null, "Secrets_Field_ObjectStorageRegion_Label", null, IntegrationSecretCategory.ObjectStorage, false, "string"),
            ("ObjectStorage:AccessKey", null, "Secrets_Field_ObjectStorageAccessKey_Label", null, IntegrationSecretCategory.ObjectStorage, true, "string"),
            ("ObjectStorage:SecretKey", null, "Secrets_Field_ObjectStorageSecretKey_Label", null, IntegrationSecretCategory.ObjectStorage, true, "string"),
            ("ObjectStorage:ServiceUrl", null, "Secrets_Field_ObjectStorageServiceUrl_Label", "Secrets_Field_ObjectStorageServiceUrl_Desc", IntegrationSecretCategory.ObjectStorage, false, "url"),
            ("ObjectStorage:PublicBaseUrl", null, "Secrets_Field_ObjectStoragePublicBaseUrl_Label", "Secrets_Field_ObjectStoragePublicBaseUrl_Desc", IntegrationSecretCategory.ObjectStorage, false, "url"),
        ];

        List<string> existingKeys = await context.IntegrationSecrets
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        var toAdd = catalog
            .Where(c => !existingKeys.Contains(c.Key))
            .Select(c => new IntegrationSecret
            {
                CreateUserId = SuperAdminId,
                Key          = c.Key,
                // A plain default (Port=587) is seeded as-is; a secret's default would
                // have to be encrypted to be readable later, and none of these fields
                // has one, so this is never reached for IsSecret=true today.
                Value        = c.Value,
                DisplayName  = c.DisplayName,
                Description  = c.Description,
                Category     = c.Category,
                IsSecret     = c.IsSecret,
                DataType     = c.DataType,
                IsSystem     = true,
            })
            .ToList();

        if (toAdd.Count == 0)
            return;

        await context.IntegrationSecrets.AddRangeAsync(toAdd, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[Seed] {Count} integration secret(s) seeded.", toAdd.Count);
    }

    /// <summary>
    /// Keys that used to hold injected code and are now rows in
    /// <c>SiteCodeSnippets</c>. Left behind they would keep showing up as editable
    /// fields on the Site Settings screen that no longer affect the public site —
    /// the worst kind of dead control, because it looks like it works.
    /// </summary>
    private static readonly string[] RetiredSettingKeys =
    [
        "Analytics.GoogleAnalyticsId",
        "Analytics.GoogleTagManagerId",
        "Analytics.FacebookPixelId",
        "Analytics.CustomHeadScript",
        "Analytics.CustomBodyScript",
        "Integrations.CookieConsentScript",
        "Seo.GoogleSiteVerification",
        // The header/footer template picker was removed from Site Settings before
        // Site Kodları existed; these two rows were left behind and, with nothing else
        // in the Appearance group, were surfacing as a fifth "Appearance" tab —
        // exactly the kind of leftover this list exists to catch.
        "Appearance.HeaderTemplateId",
        "Appearance.FooterTemplateId",
        // Same mistake, tried again and reverted in the same session: a menu is
        // ordinary content (a Menu-type template embedded into a page as a linked
        // template), not a site-wide setting. The web layer has no business knowing
        // "the header menu" exists as a concept, so this never gets to accumulate
        // rows the way the pair above did — caught and retired immediately.
        "Appearance.HeaderMenuTemplateId",
        // One free-text CSS field with no name, no on/off switch and no ordering
        // relative to anything else — exactly the shape Site Codes replaced
        // Analytics.* for. Add it there as a "Custom Code" snippet instead.
        "Appearance.GlobalCss",
    ];

    private static async Task RemoveRetiredSettingsAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        List<SiteSetting> retired = await context.SiteSettings
            .Where(s => RetiredSettingKeys.Contains(s.Key))
            .ToListAsync(cancellationToken);

        if (retired.Count == 0)
            return;

        // A key someone had actually filled in is a live tag on the public site.
        // Dropping it silently would take that tag down with no trace of what it
        // was, so those are reported and left alone for a human to move across.
        List<SiteSetting> withValues = [.. retired.Where(s => !string.IsNullOrWhiteSpace(s.Value))];
        List<SiteSetting> empty = [.. retired.Where(s => string.IsNullOrWhiteSpace(s.Value))];

        foreach (SiteSetting setting in withValues)
        {
            logger.LogWarning(
                "[Seed] Site setting '{Key}' has moved to Site Codes but still holds a value. "
                + "Re-add it there, then delete this row. Value kept for now: {Value}",
                setting.Key, setting.Value);
        }

        if (empty.Count == 0)
            return;

        context.SiteSettings.RemoveRange(empty);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[Seed] {Count} retired site setting(s) removed.", empty.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatErrors(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Description));
}
