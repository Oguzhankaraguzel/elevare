using AngleSharp;
using AngleSharp.Dom;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;
using System.Data.Common;

namespace Application.Services;

/// <summary>
/// Fills in the "Breadcrumbs" block — <c>&lt;div data-elevare-breadcrumb&gt;</c> — from
/// the page's own ancestry at request time.
/// <para>
/// The block ships with a single crumb the editor styles once; this clones it per
/// ancestor and deletes the original, so the trail always matches the real page tree
/// without anyone maintaining it by hand. Same repeater pattern as the page listing
/// block, and the reason the editor's layout survives: nothing here builds markup,
/// it only copies what is already on the canvas.
/// </para>
/// </summary>
public sealed class BreadcrumbResolutionService(IPublicReadDbContext db)
{
    private const string MarkerAttribute = "data-elevare-breadcrumb";
    private const string HomeLabelAttribute = "data-elevare-breadcrumb-home";
    private const string CrumbTemplateAttribute = "data-elevare-crumb-template";
    private const string CrumbLinkAttribute = "data-elevare-crumb-link";
    private const string SeparatorAttribute = "data-elevare-crumb-sep";

    /// <summary>Guards against a parent cycle turning the walk into an infinite loop.</summary>
    private const int MaxDepth = 20;

    /// <summary>
    /// Returns the HTML with every breadcrumb block expanded for the page identified
    /// by <paramref name="pageId"/>. Null or marker-free HTML passes through unchanged
    /// — that is a page without the block, not a failure.
    /// </summary>
    public async Task<Result<string?>> ResolveAsync(string? html, int pageId, CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveCoreAsync(html, pageId, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure<string?>(RenderErrors.ResolutionFailed("the breadcrumb trail", ex.Message));
        }
    }

    private async Task<Result<string?>> ResolveCoreAsync(string? html, int pageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains(MarkerAttribute, StringComparison.Ordinal))
            return Result.Success<string?>(html);

        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        using IDocument document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        List<IElement> blocks = [.. document.QuerySelectorAll('[' + MarkerAttribute + ']')];
        if (blocks.Count == 0)
            return Result.Success<string?>(document.Body?.InnerHtml ?? html);

        Trail trail = await BuildTrailAsync(pageId, cancellationToken);

        foreach (IElement block in blocks)
            ResolveBlock(block, trail);

        return document.Body?.InnerHtml ?? html;
    }

    private static void ResolveBlock(IElement block, Trail trail)
    {
        IElement? template = block.QuerySelector('[' + CrumbTemplateAttribute + ']');
        if (template is null)
            return; // Editor deleted the crumb; nothing to repeat, leave the block alone.

        // An empty home label is the editor's way of saying "no home crumb", so the
        // trail can start at the section instead. On the homepage itself there is
        // nothing above it to lead back to.
        string? homeLabel = block.GetAttribute(HomeLabelAttribute);
        List<Crumb> crumbs = string.IsNullOrWhiteSpace(homeLabel) || trail.IsHome
            ? [.. trail.Pages]
            : [new Crumb(homeLabel, trail.HomeUrl), .. trail.Pages];

        foreach (Crumb crumb in crumbs)
        {
            var clone = (IElement)template.Clone(true);
            clone.RemoveAttribute(CrumbTemplateAttribute);

            // The clone keeps the template's id, which duplicates it across crumbs.
            // That is deliberate and matches the page listing block: GrapesJS styles
            // by id (#ixxxx { ... } in the page CSS), so stripping ids would strip
            // the editor's styling — the one thing this pattern exists to preserve.
            // Browsers apply an id rule to every matching element, so it renders right.

            IElement? link = clone.Matches('[' + CrumbLinkAttribute + ']')
                ? clone
                : clone.QuerySelector('[' + CrumbLinkAttribute + ']');

            if (link is not null)
            {
                link.TextContent = crumb.Label;
                link.SetAttribute("href", crumb.Url);
            }

            // The last crumb is the page you are already on: a link back to itself is
            // noise for a reader and a self-referential edge for a crawler.
            if (ReferenceEquals(crumb, crumbs[^1]))
            {
                clone.QuerySelector('[' + SeparatorAttribute + ']')?.Remove();
                if (link is not null)
                {
                    link.RemoveAttribute("href");
                    link.SetAttribute("aria-current", "page");
                }
            }

            template.Parent?.InsertBefore(clone, template);
        }

        template.Remove();
    }

    /// <summary>
    /// The page's ancestors, outermost first, ending with the page itself. Only
    /// published ancestors are linked — a crumb pointing at a draft would 404.
    /// <para>
    /// The homepage is not one of them: the block's home crumb stands for it. It
    /// used to be both — the walk reached the homepage and the block put "/" in
    /// front — so a page under the homepage showed two home crumbs, and an English
    /// page began at "/" (the default language's homepage) before "/en". The home
    /// crumb now leads to the homepage of the page's own language.
    /// </para>
    /// </summary>
    private async Task<Trail> BuildTrailAsync(int pageId, CancellationToken cancellationToken)
    {
        List<Crumb> reversed = [];
        string? homeUrl = null;
        int? languageId = null;
        bool isHome = false;
        int? cursor = pageId;

        for (int depth = 0; cursor is int id && depth < MaxDepth; depth++)
        {
            PublicPage? page = await db.PageInfos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (page is null)
                break;

            bool isSelf = depth == 0;
            languageId ??= page.LanguageId;
            if (IsHomePage(page))
            {
                homeUrl = BuildPageUrl(page);
                isHome = isSelf;
                if (!isSelf) break;
            }

            if (isSelf || page.PageStatus == PublicPageStatus.Published && page.IsActive)
            {
                string label = !string.IsNullOrWhiteSpace(page.SeoTitle) ? page.SeoTitle : page.Slug;
                reversed.Add(new Crumb(label, BuildPageUrl(page)));
            }

            cursor = page.ParentPageId;
        }

        // A top-level page never walks into the homepage; ask for its language's.
        if (homeUrl is null && languageId is int language)
        {
            string? homeSlug = await db.PageInfos
                .Where(p => p.LanguageId == language && p.ParentPageId == null && p.Slug == HomeSlug)
                .Select(p => p.FullSlug)
                .FirstOrDefaultAsync(cancellationToken);
            homeUrl = homeSlug is null ? null : PathOf(homeSlug);
        }

        reversed.Reverse();
        return new Trail(reversed, homeUrl ?? DefaultHomeUrl, isHome);
    }

    private static bool IsHomePage(PublicPage page) =>
        page.ParentPageId is null && string.Equals(page.Slug, HomeSlug, StringComparison.OrdinalIgnoreCase);

    // Every published page's actual public URL is "/" + FullSlug (see
    // PageListingResolutionService.BuildPageUrl for the same convention).
#pragma warning disable S1075
    private static string BuildPageUrl(PublicPage p) => PathOf(p.FullSlug);

    private static string PathOf(string fullSlug) => "/" + fullSlug;

    private const string DefaultHomeUrl = "/";
#pragma warning restore S1075

    /// <summary>The homepage's reserved slug: always top-level, rendered at "/" (or "/en").</summary>
    private const string HomeSlug = "home";

    private sealed record Crumb(string Label, string Url);

    private sealed record Trail(IReadOnlyList<Crumb> Pages, string HomeUrl, bool IsHome);
}
