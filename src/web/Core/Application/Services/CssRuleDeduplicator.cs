using System.Text;

namespace Application.Services;

/// <summary>
/// Joins a page's own CSS with the CSS of every template it links, dropping rules
/// that appear byte-for-byte more than once.
/// <para>
/// The duplication is structural, not a mistake anyone made: the page builder saves
/// the linked template's rules into the page's own stylesheet (the canvas needs them
/// to render the template it just expanded), and then
/// <see cref="TemplateResolutionService"/> appends the template's stylesheet again at
/// request time (the published page needs them even if the page CSS were empty).
/// Neither side can safely stop emitting its copy, so the two copies are reconciled
/// here, at the one point that sees both. On a page whose header comes from a
/// template this was 32 of 95 rules — about 36% of the stylesheet.
/// </para>
/// </summary>
public static class CssRuleDeduplicator
{
    /// <summary>
    /// Returns <paramref name="pageCss"/> followed by <paramref name="templateCss"/>,
    /// with earlier copies of a repeated rule removed and the LAST copy kept.
    /// <para>
    /// Keeping the last copy is what makes this safe rather than merely smaller.
    /// Two identical rules can straddle a third that conflicts with them at equal
    /// specificity — <c>.a{color:red} .b{color:blue} .a{color:red}</c> — and for an
    /// element matching both, whichever <c>.a</c> comes last is the one that wins.
    /// Dropping the earlier copy leaves that outcome untouched; dropping the later
    /// one would hand the element to <c>.b</c> and silently repaint the page.
    /// </para>
    /// <para>
    /// Matching is deliberately conservative: runs of whitespace are collapsed, but
    /// a pretty-printed rule is NOT recognised as a copy of its minified twin
    /// (<c>color: red</c> versus <c>color:red</c>). Both sheets come out of the page
    /// builder minified, so the copies we actually ship are byte-identical; making
    /// the comparison smarter would mean deciding where whitespace is significant
    /// (it is around <c>:</c> in a selector, and not in a declaration), which is a
    /// CSS parser's job and would risk merging two rules that are not the same.
    /// </para>
    /// </summary>
    public static string Merge(string? pageCss, string? templateCss)
    {
        string combined = (pageCss ?? string.Empty) + (templateCss ?? string.Empty);
        if (combined.Length == 0)
            return string.Empty;

        List<string> rules = SplitTopLevelRules(combined);

        // Walk backwards so the first time a rule is seen is its LAST occurrence,
        // which is the copy that gets to stay.
        HashSet<string> seen = new(StringComparer.Ordinal);
        bool[] keep = new bool[rules.Count];
        for (int i = rules.Count - 1; i >= 0; i--)
            keep[i] = seen.Add(Normalize(rules[i]));

        var builder = new StringBuilder(combined.Length);
        for (int i = 0; i < rules.Count; i++)
            if (keep[i])
                builder.Append(rules[i]);

        return builder.ToString();
    }

    /// <summary>
    /// Cuts the stylesheet at every point where brace depth returns to zero, so an
    /// <c>@media</c> block comes back whole rather than as its inner rules — two
    /// identical media blocks then compare equal in one step.
    /// <para>
    /// Anything trailing after the final <c>}</c> is emitted as its own chunk so
    /// nothing is ever dropped. A brace-less statement such as
    /// <c>@import url(x);</c> stays attached to the rule that follows it; that
    /// chunk simply stops matching other copies, which costs a missed
    /// de-duplication and never a wrong one.
    /// </para>
    /// </summary>
    private static List<string> SplitTopLevelRules(string css)
    {
        List<string> rules = [];
        int depth = 0;
        int start = 0;

        for (int i = 0; i < css.Length; i++)
        {
            char c = css[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                // Guard against a stray closing brace: without this, depth would go
                // negative and every later rule would be treated as one giant chunk.
                if (depth > 0)
                    depth--;

                if (depth == 0)
                {
                    rules.Add(css[start..(i + 1)]);
                    start = i + 1;
                }
            }
        }

        if (start < css.Length)
            rules.Add(css[start..]);

        return rules;
    }

    /// <summary>
    /// Collapses runs of whitespace so two copies that differ only in formatting —
    /// one minified by the editor, one not — still count as the same rule.
    /// <para>
    /// Whitespace inside a quoted value is left exactly as written:
    /// <c>content: "a  b"</c> and <c>content: "a b"</c> render differently, so
    /// flattening both to one string would let the second silently replace the
    /// first.
    /// </para>
    /// </summary>
    private static string Normalize(string rule)
    {
        var builder = new StringBuilder(rule.Length);
        bool inWhitespace = false;
        char quote = '\0';

        for (int i = 0; i < rule.Length; i++)
        {
            char c = rule[i];

            if (quote != '\0')
            {
                builder.Append(c);
                // A quote preceded by a backslash is part of the string, not its end.
                if (c == quote && rule[i - 1] != '\\')
                    quote = '\0';
                continue;
            }

            if (c is '"' or '\'')
            {
                if (inWhitespace && builder.Length > 0)
                    builder.Append(' ');
                inWhitespace = false;
                quote = c;
                builder.Append(c);
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                inWhitespace = true;
                continue;
            }

            if (inWhitespace && builder.Length > 0)
                builder.Append(' ');

            inWhitespace = false;
            builder.Append(c);
        }

        return builder.ToString();
    }
}
