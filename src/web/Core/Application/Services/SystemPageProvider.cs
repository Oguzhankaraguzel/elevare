using System.Data.Common;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Services;

/// <summary>
/// Loads "system pages" — CMS-editable replacements for built-in pages, addressed
/// by reserved slugs:
/// <c>404</c> (not-found), <c>500</c> (server error), <c>maintenance</c> (maintenance mode).
/// Editors simply create a page with one of these slugs in the page builder and
/// publish it; when none exists (or it's still a draft) the caller falls back to
/// the built-in static page. Being ordinary pages, they can also be previewed
/// directly at /404, /500, /maintenance.
/// <para>
/// Each of these can be translated like any other page, so the lookup is
/// language-aware: a visitor who hit a dead link under <c>/en/…</c> gets the
/// English 404, not the Turkish one. See <see cref="TryGetAsync"/> for the
/// fallback order.
/// </para>
/// </summary>
public sealed class SystemPageProvider(IPublicReadDbContext db, TemplateResolutionService templateResolver)
{
    public const string NotFoundSlug = "404";
    public const string ServerErrorSlug = "500";
    public const string MaintenanceSlug = "maintenance";

    /// <summary>Fallback used when the Languages table can't name a default.</summary>
    private const string FallbackDefaultLanguageCode = "tr";

    /// <summary>
    /// Returns the published page's resolved content, or a null value when the page
    /// doesn't exist / isn't published / has no content — all three are normal, and
    /// mean "use the built-in fallback".
    /// <para>
    /// <paramref name="languageCode"/> is the language the visitor was browsing in.
    /// The page is looked up in that language first and, when it has no translation
    /// there, in the site's default language — an untranslated 404 page in the wrong
    /// language still beats the built-in static one, which is in no language the
    /// editor chose at all.
    /// </para>
    /// <para>
    /// A failure result means something else: the lookup itself broke. That matters
    /// here more than anywhere, because this method renders the 404 and 500 pages —
    /// letting it throw would mean the error page's own failure replaces the error the
    /// visitor was supposed to see.
    /// </para>
    /// </summary>
    public async Task<Result<SystemPageContent?>> TryGetAsync(
        string slug,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return await TryGetCoreAsync(slug, languageCode, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure<SystemPageContent?>(
                RenderErrors.ResolutionFailed($"'{slug}' system page", ex.Message));
        }
    }

    private async Task<Result<SystemPageContent?>> TryGetCoreAsync(
        string slug,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        // PageInfo.ComputeFullSlug (CMS side) omits the language prefix for the
        // default language and includes it for every other one — the same
        // reconstruction GetPublicPageBySlugQueryHandler does, so both sides agree
        // no matter which language is currently marked default.
        string defaultCode = await db.Languages
            .Where(l => l.IsDefault && l.IsActive)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? FallbackDefaultLanguageCode;

        // Most specific first; the default-language page is always the last resort.
        List<string> candidateSlugs = [];
        if (!string.IsNullOrWhiteSpace(languageCode)
            && !string.Equals(languageCode, defaultCode, StringComparison.OrdinalIgnoreCase))
        {
            candidateSlugs.Add($"{languageCode}/{slug}");
        }
        candidateSlugs.Add(slug);

        // One round trip for both candidates; preference is applied in memory so the
        // ordering stays explicit rather than depending on how the DB sorts.
        List<PublicPage> matches = await db.PageInfos
            .Include(p => p.Content)
            .Where(p => candidateSlugs.Contains(p.FullSlug)
                        && p.PageStatus == PublicPageStatus.Published
                        && p.IsActive)
            .ToListAsync(cancellationToken);

        PublicPage? page = candidateSlugs
            .Select(candidate => matches.Find(p => p.FullSlug == candidate))
            .FirstOrDefault(p => p is not null);

        if (page?.Content?.GjsHtml is not { Length: > 0 } html)
            return Result.Success<SystemPageContent?>(null);

        Result<ResolvedTemplateContent> resolved = await templateResolver.ResolveAsync(html, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<SystemPageContent?>(resolved.Error);

        if (string.IsNullOrWhiteSpace(resolved.Value.Html))
            return Result.Success<SystemPageContent?>(null);

        // A 404 or 500 page is a page like any other to the site codes: what its
        // author switched off there stays off.
        HashSet<int> excluded = await SiteCodeExclusions.ForPageAsync(db, page.Id, cancellationToken);

        return Result.Success<SystemPageContent?>(
            new SystemPageContent(resolved.Value.Html, CssRuleDeduplicator.Merge(page.Content.GjsCss, resolved.Value.Css), excluded));
    }
}
