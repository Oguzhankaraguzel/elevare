using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using SharedKernel.Content;

namespace Application.Services;

/// <summary>
/// The last touches a rendered page gets from what its blocks mark, all of them
/// things only the server can do well: colouring the code in "Kod Bloğu" blocks
/// (<see cref="CodeHighlighter"/> — no highlighting script is shipped), the colours
/// themselves (written once, only on pages that have code), an Article's reading
/// time from the words actually in it, and the builder's own default wording in the
/// page's language (<see cref="BlockTextLocalizer"/>). Pure string in, string out, like
/// <see cref="PageRegionSplitter"/>: unparseable content comes back unchanged.
/// </summary>
public static partial class ContentEnhancer
{
    private const int WordsPerMinute = 200;
    private const string ThemeMarker = "data-elevare-code-theme";

    /// <summary>
    /// VS Code's "Dark+" palette — the one most readers of code already know — and
    /// the block's frame. Scoped to the block, so nothing else on the page changes.
    /// </summary>
    private const string CodeTheme =
        ".elevare-code{border-radius:10px;overflow:hidden;background:#1e1e1e;color:#d4d4d4;margin:20px 0;font-size:14px;text-align:left}" +
        ".elevare-code .el-code-head{display:flex;align-items:center;gap:10px;padding:8px 14px;background:#2d2d2d;color:#a0a0a0;font-size:12px;font-family:inherit}" +
        ".elevare-code .el-code-lang{font-weight:700;text-transform:uppercase;letter-spacing:.04em}" +
        ".elevare-code .el-code-file{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;color:#cfcfcf}" +
        ".elevare-code .el-code-copy{margin-left:auto;background:transparent;border:1px solid #4a4a4a;color:#cfcfcf;border-radius:6px;padding:3px 10px;font-size:12px;cursor:pointer}" +
        ".elevare-code .el-code-copy:hover{border-color:#8a8a8a;color:#fff}" +
        ".elevare-code pre{margin:0;padding:16px 18px;overflow:auto;line-height:1.6;background:transparent;tab-size:4}" +
        ".elevare-code code{font-family:ui-monospace,SFMono-Regular,Consolas,\"Cascadia Code\",Menlo,monospace;background:none;color:inherit;padding:0;font-size:inherit;white-space:pre}" +
        ".elevare-code .tk-k,.elevare-code .tk-g{color:#569cd6}.elevare-code .tk-s{color:#ce9178}.elevare-code .tk-c{color:#6a9955;font-style:italic}" +
        ".elevare-code .tk-n{color:#b5cea8}.elevare-code .tk-t{color:#4ec9b0}.elevare-code .tk-f{color:#dcdcaa}.elevare-code .tk-p{color:#9cdcfe}" +
        ".elevare-code[data-line-numbers=\"true\"] code{counter-reset:el-ln}" +
        ".elevare-code .el-code-line{display:inline}" +
        ".elevare-code[data-line-numbers=\"true\"] .el-code-line::before{counter-increment:el-ln;content:counter(el-ln);display:inline-block;width:2.4em;margin-right:1em;text-align:right;color:#6e7681;user-select:none}";

    public static async Task<string?> EnhanceAsync(string? html, string languageCode, CancellationToken cancellationToken)
    {
        // Everything below keys on the builder's data-elevare-* markers.
        if (string.IsNullOrWhiteSpace(html) || !BlockTextLocalizer.MayApply(html))
            return html;

        try
        {
            using IDocument document = await BrowsingContext.New(Configuration.Default)
                .OpenAsync(req => req.Content(html), cancellationToken);
            HighlightCode(document);
            FillReadingTimes(document, languageCode);
            BlockTextLocalizer.Apply(document, languageCode);
            return document.Body?.InnerHtml ?? html;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            return html;
        }
    }

    private static void HighlightCode(IDocument document)
    {
        List<IElement> blocks = [.. document.QuerySelectorAll("[data-elevare-code]")];
        if (blocks.Count == 0)
            return;

        foreach (IElement block in blocks)
        {
            IElement? code = block.QuerySelector("pre code") ?? block.QuerySelector("code");
            if (code is null)
                continue;
            string language = CodeHighlighter.Normalize(block.GetAttribute("data-language"));
            string source = code.TextContent.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
            code.InnerHtml = SplitLines(CodeHighlighter.Highlight(source, language));
            code.ClassName = "language-" + language;
        }

        if (document.QuerySelector($"style[{ThemeMarker}]") is null)
        {
            IElement style = document.CreateElement("style");
            style.SetAttribute(ThemeMarker, "");
            style.TextContent = CodeTheme;
            blocks[0].Before(style);
        }
    }

    // Each line in its own element, so line numbers can be counted in CSS. The
    // highlighter's spans never nest, so a token running over a line break (a block
    // comment, a verbatim string) is closed at the break and reopened after it.
    private static string SplitLines(string highlighted)
    {
        var sb = new StringBuilder(highlighted.Length + 64);
        sb.Append("<span class=\"el-code-line\">");
        string? openToken = null;
        int i = 0;
        while (i < highlighted.Length)
        {
            if (highlighted[i] == '\n')
            {
                if (openToken is not null) sb.Append("</span>");
                sb.Append("</span>\n<span class=\"el-code-line\">");
                if (openToken is not null) sb.Append(openToken);
                i++;
                continue;
            }
            if (highlighted[i] == '<')
            {
                int close = highlighted.IndexOf('>', i);
                string tag = highlighted[i..(close + 1)];
                openToken = tag.StartsWith("</", StringComparison.Ordinal) ? null : tag;
                sb.Append(tag);
                i = close + 1;
                continue;
            }
            sb.Append(highlighted[i]);
            i++;
        }
        sb.Append("</span>");
        return sb.ToString();
    }

    private static void FillReadingTimes(IDocument document, string languageCode)
    {
        foreach (IElement slot in document.QuerySelectorAll("[data-elevare-article-reading-time]"))
        {
            IElement scope = slot.Closest("[data-elevare-article]") ?? document.Body!;
            IElement body = scope.QuerySelector("[data-elevare-article-body]") ?? scope;
            int words = Words().Count(body.TextContent);
            int minutes = Math.Max(1, (int)Math.Round(words / (double)WordsPerMinute));
            slot.TextContent = languageCode.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
                ? $"{minutes} dk okuma"
                : $"{minutes} min read";
        }
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’][\p{L}]+)?")]
    private static partial Regex Words();
}
