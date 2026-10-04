using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace Application.Services;

/// <summary>
/// Keeps each GrapesJS component script once on a page. The builder exports a
/// component's behaviour as a segment of the form
/// <c>var items=document.querySelectorAll('#id');for(…){(function(){…}.bind(items[i]))();}</c>
/// inside one <c>&lt;script&gt;</c> at the end of the exported HTML — and a page
/// that embeds a linked template carries the template's components in its own
/// export too (the locked copy shown in the editor), so its script block already
/// has segments for the template's ids. When the template is substituted in at
/// render time, its own script block arrives with it: the same ids, bound a second
/// time. Two click handlers on the theme switch toggled dark→light→dark and the
/// button appeared to do nothing; the search box and the mobile menu toggle were
/// double-bound the same way. Measured on the live homepage.
/// <para>
/// Segments are keyed by the id they select; the first occurrence of each is kept
/// and later ones dropped, across every script block on the page. Scripts that are
/// not in this shape — site codes, the layout's own — are left untouched.
/// </para>
/// </summary>
public static partial class ComponentScriptDeduplicator
{
    public static void Apply(IDocument document)
    {
        HashSet<string> seen = [];
        foreach (IElement script in document.QuerySelectorAll("script").ToList())
        {
            if (script.HasAttribute("src") || script.HasAttribute("type")) continue;
            string text = script.TextContent;
            if (!SegmentStartRegex().IsMatch(text)) continue;

            var kept = new StringBuilder();
            foreach (Match segment in SegmentRegex().Matches(text))
            {
                if (seen.Add(segment.Groups["id"].Value))
                    kept.Append(segment.Value);
            }

            if (kept.Length == 0) script.Remove();
            else if (kept.Length != text.Length) script.TextContent = kept.ToString();
        }
    }

    // Whitespace-tolerant on purpose. The builder's export reads
    // `var items = document.querySelectorAll('#id')`, with spaces; the page as
    // served reads `var items=document.querySelectorAll('#id')`, because the HTML
    // minifier has been over it by then. This runs before the minifier — a first
    // version matched the served form, proved itself against the served page, and
    // did nothing in production.
    private const string Selector = @"var\s+items\s*=\s*document\.querySelectorAll\(\s*'#(?<id>[\w-]+)'\s*\)";

    [GeneratedRegex(@"^\s*" + Selector)]
    private static partial Regex SegmentStartRegex();

    // One segment: from its selector up to the next segment's selector, or the end.
    [GeneratedRegex(Selector + @".*?(?=var\s+items\s*=\s*document\.querySelectorAll\(\s*'#|\z)", RegexOptions.Singleline)]
    private static partial Regex SegmentRegex();
}
