using AngleSharp;
using AngleSharp.Dom;

namespace Application.Services;
/// <summary>
/// Lifts a page's top-level <c>&lt;header&gt;</c> and <c>&lt;footer&gt;</c> out of its
/// content so the layout can place them OUTSIDE <c>&lt;main&gt;</c>.
/// <para>
/// Everything a page builder produces lands inside <c>&lt;main id="main-content"&gt;</c>,
/// which means a site's menu and footer end up nested in it. That is the wrong
/// document outline: <c>&lt;header&gt;</c> only becomes the page's <c>banner</c>
/// landmark, and <c>&lt;footer&gt;</c> its <c>contentinfo</c>, when they are NOT inside
/// <c>main</c>/<c>article</c>/<c>section</c>. Nested, they are generic boxes, and the
/// content that repeats on every page counts as part of each page's own content.
/// </para>
/// <para>
/// Detection is by TAG, deliberately — not by which template a block came from. A menu
/// is a menu whether it arrived through a linked template or was dragged straight onto
/// the page, and the blocks already emit the right elements (the Navbar block's root is
/// <c>&lt;header&gt;</c>, the Footer block's is <c>&lt;footer&gt;</c>).
/// </para>
/// </summary>
public static class PageRegionSplitter
{
    /// <summary>
    /// Splits <paramref name="html"/> into header/main/footer.
    /// <para>
    /// Only DIRECT children of the fragment are moved. That is the whole rule, and it
    /// is what keeps a nested <c>&lt;header&gt;</c> where it belongs — the Article
    /// block puts its byline in one, inside <c>&lt;article&gt;</c>, and that header is
    /// the article's, not the site's. By the same rule a menu someone dropped inside a
    /// section stays put: a header nested in a section is not the page banner either.
    /// </para>
    /// <para>
    /// Pure string-in, string-out over HTML this request has already parsed several
    /// times, so nothing here is worth a <c>Result</c>: unparseable content simply
    /// comes back as one undivided main region, which is exactly what the page did
    /// before this existed.
    /// </para>
    /// </summary>
    public static async Task<PageRegions> SplitAsync(string? html, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new PageRegions(string.Empty, html ?? string.Empty, string.Empty);

        try
        {
            IBrowsingContext context = BrowsingContext.New(Configuration.Default);
            using IDocument document = await context.OpenAsync(req => req.Content(html), cancellationToken);

            IElement? body = document.Body;
            if (body is null)
                return new PageRegions(string.Empty, html, string.Empty);

            // Snapshotted before removing anything: the child list is live.
            List<IElement> topLevel = [.. body.Children];

            string header = Extract(topLevel, "header");
            string footer = Extract(topLevel, "footer");

            return new PageRegions(header, body.InnerHtml, footer);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new PageRegions(string.Empty, html, string.Empty);
        }
    }

    /// <summary>
    /// Takes every top-level element with the given tag out of the document and
    /// returns their markup, in the order they appeared.
    /// </summary>
    private static string Extract(List<IElement> topLevel, string tagName)
    {
        List<IElement> matches = [.. topLevel.Where(e =>
            string.Equals(e.LocalName, tagName, StringComparison.OrdinalIgnoreCase))];

        if (matches.Count == 0)
            return string.Empty;

        // All of them, not just the first. A page with two menus keeps both, in order —
        // dropping the extras would silently delete content someone put there on
        // purpose, which is worse than an unusual outline.
        string markup = string.Concat(matches.Select(m => m.OuterHtml));
        foreach (IElement match in matches)
            match.Remove();

        return markup;
    }
}
