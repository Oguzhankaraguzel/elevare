using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Application.Features.Queries.Search.SearchPublicContent;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;
using System.Data.Common;

namespace Application.Services;

/// <summary>
/// Default <see cref="ISearchProvider"/> — matches published pages by title/slug
/// AND visible page content (stripped of markup via AngleSharp), scoped to the
/// given language. A SQL <c>Contains()</c> prefilter narrows candidates cheaply
/// (it also matches inside markup/attributes, e.g. a class name); the real
/// "did the visible text match" check happens after the HTML-to-text pass, so a
/// class="search-box" false positive never reaches a visitor.
/// </summary>
internal sealed class SqlSearchProvider(IPublicReadDbContext db) : ISearchProvider
{
    private const int CandidateCap = 60;
    private const int ExcerptContextChars = 60;

    public async Task<Result<List<SearchResultItemResponse>>> SearchAsync(
        string term, string languageCode, int maxResults, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteSearchAsync(term, languageCode, maxResults, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            // Reported rather than returned as an empty list: "we could not search" and
            // "there is nothing to find" must not look the same to the visitor.
            return Result.Failure<List<SearchResultItemResponse>>(SearchErrors.Failed(ex.Message));
        }
    }

    private async Task<Result<List<SearchResultItemResponse>>> ExecuteSearchAsync(
        string term, string languageCode, int maxResults, CancellationToken cancellationToken)
    {
        int languageId = await db.Languages
            .Where(PublicLanguage.PubliclyVisible)
            .Where(l => l.TwoLetterCode == languageCode)
            .Select(l => l.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (languageId == 0)
            return Result.Success<List<SearchResultItemResponse>>([]);

        // Lower-cased on BOTH sides so the database compares case-insensitively.
        //
        // string.Contains translates to LIKE '%term%', and LIKE in PostgreSQL is
        // case-SENSITIVE — so searching "Hakkımızda" found the page and "hakkımızda"
        // found nothing. The check further down already used OrdinalIgnoreCase, which
        // made this especially easy to miss: the code read as case-insensitive, but
        // the rows never survived the prefilter to reach it.
        //
        // ToLower rather than ILIKE because ILIKE is PostgreSQL-specific and reaching
        // for it here would pull the database provider into the Application layer.
        //
        // This is NOT free, and it would be wrong to say otherwise: these columns
        // carry pg_trgm GIN indexes (AddSearchAndPaginationIndexes) precisely so that
        // a '%term%' pattern CAN use an index, and an index on the bare column does
        // not serve lower(column). The indexes were re-pointed at lower(column) to
        // match — see SearchIndexesCaseInsensitive.
#pragma warning disable CA1304, CA1308, CA1311, CA1862
        string loweredTerm = SearchTerm.LowerLikeDatabase(term);

        // In the stored HTML a phrase is often broken up by markup — "<h2>Portakal</h2>
        // <p>Bahçesi", "<b>Kalp</b> sağlığı" — so the content side of the prefilter
        // looks for the phrase's longest word only. Whether the whole phrase is
        // actually there is decided on the visible text below.
        string contentAnchor = loweredTerm
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .OrderByDescending(w => w.Length)
            .FirstOrDefault() ?? loweredTerm;

        // CA1304/CA1308/CA1311/CA1862 are suppressed, not obeyed: this is an EF Core expression tree,
        // not in-memory string work. The StringComparison overloads CA1862 recommends
        // cannot be translated to SQL (EF throws at runtime), and ToLowerInvariant —
        // CA1311's fix — has no SQL mapping either. Plain ToLower() is the ONLY form
        // that becomes lower() in the query, which is the whole point here.
        // CA1308 (prefer upper-casing) does not apply either: the comparison has to
        // agree with SQL lower(), so the term must be lower-cased to match.
        List<PublicPage> candidates = await db.PageInfos
            .AsNoTracking()
            .Include(p => p.Content)
            .Where(p => p.LanguageId == languageId && p.PageStatus == PublicPageStatus.Published && p.IsActive)
            .Where(p => p.SeoTitle.ToLower().Contains(loweredTerm)
                || p.Slug.ToLower().Contains(loweredTerm)
                || p.Content != null && p.Content.GjsHtml != null && p.Content.GjsHtml.ToLower().Contains(contentAnchor))
            .OrderByDescending(p => p.CreateDate)
            .Take(CandidateCap)
            .ToListAsync(cancellationToken);
#pragma warning restore CA1304, CA1308, CA1311, CA1862

        if (candidates.Count == 0)
            return Result.Success<List<SearchResultItemResponse>>([]);

        Dictionary<int, string> parentTitles = await ResolveParentTitlesAsync(candidates, cancellationToken);

        List<(PublicPage Page, bool TitleMatch, string Excerpt)> matches = [];
        foreach (PublicPage page in candidates)
        {
            bool titleMatch = page.SeoTitle.Contains(term, StringComparison.OrdinalIgnoreCase)
                || page.Slug.Contains(term, StringComparison.OrdinalIgnoreCase);

            string plainText = await StripHtmlAsync(page.Content?.GjsHtml, cancellationToken);
            int contentIndex = plainText.IndexOf(term, StringComparison.OrdinalIgnoreCase);

            if (!titleMatch && contentIndex < 0)
                continue;

            // A title match is best summed up by the page's own description; the text
            // around the first hit in the body is for matches the title does not explain.
            string excerpt = page.SeoMetaDescription;
            bool describedByTitle = titleMatch && !string.IsNullOrWhiteSpace(page.SeoMetaDescription);
            if (!describedByTitle && contentIndex >= 0)
                excerpt = BuildExcerpt(plainText, term, contentIndex);

            matches.Add((page, titleMatch, excerpt));
        }

        return Result.Success<List<SearchResultItemResponse>>([.. matches
            .OrderByDescending(m => m.TitleMatch)
            .ThenByDescending(m => m.Page.CreateDate)
            .Take(maxResults)
            .Select(m => new SearchResultItemResponse(
                Title: !string.IsNullOrWhiteSpace(m.Page.SeoTitle) ? m.Page.SeoTitle : m.Page.Slug,
                Path: BuildPageUrl(m.Page),
                Excerpt: m.Excerpt,
                Category: m.Page.ParentPageId.HasValue && parentTitles.TryGetValue(m.Page.ParentPageId.Value, out string? title)
                    ? title
                    : null))]);
    }

    private async Task<Dictionary<int, string>> ResolveParentTitlesAsync(List<PublicPage> candidates, CancellationToken cancellationToken)
    {
        List<int> parentIds = [.. candidates.Where(p => p.ParentPageId.HasValue).Select(p => p.ParentPageId!.Value).Distinct()];
        if (parentIds.Count == 0)
            return [];

        return await db.PageInfos
            .AsNoTracking()
            .Where(p => parentIds.Contains(p.Id))
            .Select(p => new { p.Id, Title = !string.IsNullOrWhiteSpace(p.SeoTitle) ? p.SeoTitle : p.Slug })
            .ToDictionaryAsync(p => p.Id, p => p.Title, cancellationToken);
    }

    // Never read by a visitor: code, embedded documents, and the page builder's own
    // machinery — linked header/footer templates (the same menu on every page would
    // make every page match "Ana Sayfa"), the search box's hidden result templates,
    // anything marked hidden or decorative (a slider's ❮ ❯ arrows), and preview
    // cards an older page listing block saved into the page.
    private const string NotReadSelector =
        "script, style, noscript, template, svg, iframe, object, canvas, " +
        "[hidden], [aria-hidden='true'], .elevare-tpl-ref, " +
        ".elevare-search-templates, .elevare-search-results-output, [data-elevare-ghost]";

    // Elements that start a new run of text on the page. TextContent glues their text
    // straight together ("İşlemlerYazar:"), so each gets a space on either side.
    private const string BreakSelector =
        "address, article, aside, blockquote, br, dd, details, div, dl, dt, fieldset, " +
        "figcaption, figure, footer, form, h1, h2, h3, h4, h5, h6, header, hr, li, main, " +
        "nav, ol, p, pre, section, summary, table, td, th, tr, ul, a, button, label, option";

    private static async Task<string> StripHtmlAsync(string? html, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        using IDocument document = await context.OpenAsync(req => req.Content(html), cancellationToken);
        if (document.Body is null)
            return "";

        foreach (IElement hidden in document.Body.QuerySelectorAll(NotReadSelector).ToList())
            hidden.Remove();
        foreach (IElement block in document.Body.QuerySelectorAll(BreakSelector))
        {
            block.Before(document.CreateTextNode(" "));
            block.After(document.CreateTextNode(" "));
        }

        return Regex.Replace(document.Body.TextContent, @"\s+", " ").Trim();
    }

    // Every published page's actual public URL is "/" + FullSlug (see
    // PageListingResolutionService.BuildPageUrl for the same convention).
#pragma warning disable S1075
    private static string BuildPageUrl(PublicPage p) => "/" + p.FullSlug;
#pragma warning restore S1075

    private static string BuildExcerpt(string plainText, string term, int matchIndex)
    {
        int start = Math.Max(0, matchIndex - ExcerptContextChars);
        int end = Math.Min(plainText.Length, matchIndex + term.Length + ExcerptContextChars);
        // Widen to whole words: a snippet that opens on "…isplay" or closes on "üre…"
        // reads as broken even when the text itself is fine.
        if (start > 0)
        {
            int space = plainText.LastIndexOf(' ', start);
            start = space >= 0 ? space + 1 : 0;
        }
        if (end < plainText.Length)
        {
            int space = plainText.IndexOf(' ', end);
            end = space >= 0 ? space : plainText.Length;
        }
        string snippet = plainText[start..end];
        if (start > 0) snippet = "…" + snippet;
        if (end < plainText.Length) snippet += "…";
        return snippet;
    }
}
