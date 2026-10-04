using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;

namespace SharedKernel.Content;

/// <summary>
/// What a page's own content says about it, for places that describe the page
/// without showing it: its card in a page listing on the public site and the
/// listing preview in the CMS editor. One reading, so the two never pick a
/// different image or summary for the same page.
/// </summary>
public static partial class PageContentFacts
{
    // Anything that is not the page's own content: linked header/footer templates and
    // a page-level menu (their logo is not what the page is about), and what is
    // hidden or decorative. Top level only — an Article block has a <header> of its
    // own, holding the byline the date is read from.
    private const string NotContentSelector =
        ".elevare-tpl-ref, body > header, body > footer, body > nav, " +
        "script, noscript, template, [hidden], [aria-hidden='true']";

    // Below this an image is an icon or an avatar, not something to show a page by.
    private const int MinImageWidth = 200;

    /// <summary>Parses <paramref name="html"/> with everything that is not the page's own content removed.</summary>
    public static async Task<IDocument?> ParseAsync(string? html, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        IDocument document = await BrowsingContext.New(Configuration.Default)
            .OpenAsync(req => req.Content(html), cancellationToken);
        foreach (IElement element in document.QuerySelectorAll(NotContentSelector).ToList())
            element.Remove();
        return document;
    }

    /// <summary>
    /// The first image that shows what the page is about: not an inline data:
    /// placeholder (what an untouched block ships with), not an SVG (no share card
    /// or search result shows one), not an icon.
    /// </summary>
    public static string? FirstImage(IDocument? content)
    {
        if (content is null)
            return null;
        foreach (IElement img in content.QuerySelectorAll("img[src]"))
        {
            string src = img.GetAttribute("src")!.Trim();
            if (src.Length == 0 || IsPlaceholder(src) || src.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                continue;
            if (int.TryParse(img.GetAttribute("width"), out int width) && width < MinImageWidth)
                continue;
            return src;
        }
        return null;
    }

    /// <summary>True for an inline placeholder image, never a real picture of anything.</summary>
    public static bool IsPlaceholder(string? src) =>
        src is not null && src.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A summary for a page with no description of its own: the Article block's
    /// opening paragraph, else the first paragraph long enough to say something —
    /// cut on a word boundary.
    /// </summary>
    public static string? OpeningText(IDocument? content, int maxLength)
    {
        if (content is null)
            return null;
        IElement? opening = content.QuerySelector("[data-elevare-article-body] p");
        string? text = Clean(opening?.TextContent);
        if (text is null or { Length: < MinSummaryLength })
        {
            text = content.QuerySelectorAll("main p, section p, article p, p")
                .Select(p => Clean(p.TextContent))
                .FirstOrDefault(t => t is { Length: >= MinSummaryLength });
        }
        return Truncate(text, maxLength);
    }

    // Shorter than this, a paragraph is a label or a byline, not a summary.
    private const int MinSummaryLength = 40;

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Whitespace().Replace(text, " ").Trim();

    private static string? Truncate(string? text, int maxLength)
    {
        if (text is null || text.Length <= maxLength)
            return text;
        int cut = text.LastIndexOf(' ', maxLength);
        return (cut > maxLength / 2 ? text[..cut] : text[..maxLength]).TrimEnd(',', ';', ':', ' ') + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
