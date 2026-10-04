using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Domain.Entities.SiteCodeSnippets;
using SharedKernel.Concrete;

namespace Application.Security;

/// <summary>
/// Decides whether a pasted snippet is safe to write into every public page.
/// <para>
/// The person adding a tag is usually not the person who wrote it — they copied it
/// from a vendor's setup page and are trusting the CMS to tell them if something is
/// off. These checks catch the handful of mistakes that actually take a site down,
/// and deliberately stop there: this is not a sandbox, and it is not trying to be.
/// Authoring code here is a privilege gated by <c>CustomCode.Author</c>, and the
/// About page states plainly that the holder can inject anything.
/// </para>
/// </summary>
public static partial class SiteCodeValidator
{
    /// <summary>
    /// Beyond this, someone is pasting a library rather than a tag. Big payloads
    /// belong in a file behind a <c>src</c> attribute — inline they are re-sent with
    /// every single page view and can never be cached.
    /// </summary>
    public const int MaxContentLength = 64 * 1024;

    /// <summary>Runs every check. Warnings do not block the save; they are shown next to the field.</summary>
    public static SiteCodeValidationResult Validate(SiteCodeKind kind, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return SiteCodeValidationResult.Invalid(SiteCodeSnippetErrors.ContentRequired);

        if (content.Length > MaxContentLength)
            return SiteCodeValidationResult.Invalid(SiteCodeSnippetErrors.TooLarge);

        if (WholeDocumentRegex().IsMatch(content))
            return SiteCodeValidationResult.Invalid(SiteCodeSnippetErrors.WholeDocumentPasted);

        if (!IsWellFormed(content))
            return SiteCodeValidationResult.Invalid(SiteCodeSnippetErrors.Malformed);

        Error? mismatch = CheckKind(kind, content);
        if (mismatch is not null)
            return SiteCodeValidationResult.Invalid(mismatch);

        // Not fatal, but worth saying out loud: document.write on an
        // asynchronously-loaded page replaces the whole document with the written
        // string, wiping the rendered site. Vendors still ship snippets that use it.
        List<string> warnings = [];
        if (content.Contains("document.write", StringComparison.OrdinalIgnoreCase))
            warnings.Add("DocumentWrite");

        return SiteCodeValidationResult.Valid(warnings);
    }

    /// <summary>
    /// Elements that never take a closing tag, so an opener alone is correct.
    /// </summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img",
        "input", "link", "meta", "param", "source", "track", "wbr",
    };

    /// <summary>
    /// An unclosed tag is the one mistake that reliably destroys a page: everything
    /// after it is swallowed as content of the tag that never ended.
    /// </summary>
    private static bool IsWellFormed(string content)
    {
        try
        {
            var parser = new HtmlParser();
            using IHtmlDocument document = parser.ParseDocument($"<div id=\"elevare-probe\">{content}</div>");

            // A parser that cannot even produce the host element is not something to serve.
            if (document.GetElementById("elevare-probe") is null)
                return false;
        }
        catch (Exception)
        {
            return false;
        }

        return HasBalancedTags(content);
    }

    /// <summary>
    /// Counting tags in the source rather than in the parsed tree, because the parser
    /// silently repairs: a lone <c>&lt;script&gt;</c> still comes back as a script
    /// element, so the tree cannot tell a closed tag from an unclosed one. Comments
    /// and complete script/style bodies are removed first so that JavaScript and CSS —
    /// which may legitimately contain angle brackets — never reach the counter.
    /// </summary>
    private static bool HasBalancedTags(string content)
    {
        string markup = HtmlCommentRegex().Replace(content, " ");
        markup = RawTextBlockRegex().Replace(markup, " ");

        Dictionary<string, int> depthByName = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TagRegex().Matches(markup))
        {
            string name = match.Groups["name"].Value;

            if (VoidElements.Contains(name) || match.Value.EndsWith("/>", StringComparison.Ordinal))
                continue;

            bool isClosing = match.Groups["slash"].Value.Length > 0;

            depthByName.TryGetValue(name, out int depth);
            depthByName[name] = depth + (isClosing ? -1 : 1);
        }

        return depthByName.Values.All(depth => depth == 0);
    }

    private static Error? CheckKind(SiteCodeKind kind, string content)
    {
        var parser = new HtmlParser();
        using IHtmlDocument document = parser.ParseDocument($"<div id=\"elevare-probe\">{content}</div>");
        IElement host = document.GetElementById("elevare-probe")!;

        bool hasScript = host.QuerySelectorAll("script").Length > 0;
        bool hasStyle = host.QuerySelectorAll("style").Length > 0
            || host.QuerySelectorAll("link[rel=stylesheet]").Length > 0;
        int metaCount = host.QuerySelectorAll("meta").Length;

        return kind switch
        {
            SiteCodeKind.Script when !hasScript =>
                SiteCodeSnippetErrors.KindMismatch("a <script> tag"),

            SiteCodeKind.Script when hasStyle =>
                SiteCodeSnippetErrors.KindMismatch("only script — this also contains styles, add those as a separate Style snippet"),

            SiteCodeKind.Style when !hasStyle =>
                SiteCodeSnippetErrors.KindMismatch("a <style> block or <link rel=\"stylesheet\">"),

            SiteCodeKind.Style when hasScript =>
                SiteCodeSnippetErrors.KindMismatch("only styles — this also contains a script, add that as a separate Script snippet"),

            SiteCodeKind.MetaTag when metaCount == 0 =>
                SiteCodeSnippetErrors.KindMismatch("a <meta> tag"),

            SiteCodeKind.MetaTag when metaCount > 1 =>
                SiteCodeSnippetErrors.KindMismatch("a single <meta> tag — add the others separately so each can be switched off on its own"),

            _ => null,
        };
    }

    /// <summary>Structural tags that only appear when a whole page was copied.</summary>
    [GeneratedRegex(@"<\s*/?\s*(html|head|body)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WholeDocumentRegex();

    /// <summary>Any tag, opening or closing, with its element name captured.</summary>
    [GeneratedRegex(@"<(?<slash>/?)(?<name>[a-zA-Z][a-zA-Z0-9-]*)(?:\s[^>]*)?>")]
    private static partial Regex TagRegex();

    /// <summary>HTML comments — their contents are not markup.</summary>
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();

    /// <summary>
    /// Complete script/style pairs. Their bodies are JavaScript and CSS, where
    /// <c>&lt;</c> is an operator rather than a tag. An unclosed opener does not match
    /// here, which is exactly what leaves it behind to be counted.
    /// </summary>
    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RawTextBlockRegex();
}
