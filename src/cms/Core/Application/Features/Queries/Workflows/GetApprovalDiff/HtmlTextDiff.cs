using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Application.Features.Queries.Workflows.GetApprovalDiff;

/// <summary>
/// Compares two GrapesJS HTML exports the way a reviewer reads them: as the words a
/// visitor would see.
/// <para>
/// Deliberately not a diff of the markup. GrapesJS rewrites ids and class names on
/// almost every save, so a raw HTML diff paints the whole page red and green and the
/// one sentence that actually changed is lost in it. An approver is being asked
/// "should this go live", and that question is about the copy.
/// </para>
/// </summary>
internal static partial class HtmlTextDiff
{
    /// <summary>
    /// A page longer than this is already past the point where reading a diff helps,
    /// and the O(n*m) table below has to stay bounded.
    /// </summary>
    private const int MaxLines = 1500;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static List<ApprovalDiffLine> Compare(string? oldHtml, string? newHtml)
    {
        List<string> before = ExtractLines(oldHtml);
        List<string> after = ExtractLines(newHtml);
        return Diff(before, after);
    }

    /// <summary>Visible text, one entry per text node, in document order.</summary>
    private static List<string> ExtractLines(string? html)
    {
        List<string> lines = [];
        if (string.IsNullOrWhiteSpace(html))
            return lines;

        HtmlParser parser = new();
        using IDocument document = parser.ParseDocument(html);
        if (document.Body is null)
            return lines;

        foreach (INode node in Descendants(document.Body))
        {
            if (node.NodeType != NodeType.Text)
                continue;

            // Script and style bodies are text nodes too, and their contents are not
            // something a reviewer is reading.
            string parent = node.ParentElement?.TagName ?? "";
            if (parent is "SCRIPT" or "STYLE" or "NOSCRIPT")
                continue;

            string text = WhitespaceRegex().Replace(node.TextContent, " ").Trim();
            if (text.Length == 0)
                continue;

            lines.Add(text);
            if (lines.Count == MaxLines)
                break;
        }

        return lines;
    }

    private static IEnumerable<INode> Descendants(INode root)
    {
        foreach (INode child in root.ChildNodes)
        {
            yield return child;
            foreach (INode nested in Descendants(child))
                yield return nested;
        }
    }

    /// <summary>Classic LCS line diff — same shape as `diff` itself.</summary>
    private static List<ApprovalDiffLine> Diff(List<string> before, List<string> after)
    {
        int[][] lcs = new int[before.Count + 1][];
        for (int i = 0; i <= before.Count; i++)
            lcs[i] = new int[after.Count + 1];

        for (int i = before.Count - 1; i >= 0; i--)
        {
            for (int j = after.Count - 1; j >= 0; j--)
            {
                lcs[i][j] = before[i] == after[j]
                    ? lcs[i + 1][j + 1] + 1
                    : Math.Max(lcs[i + 1][j], lcs[i][j + 1]);
            }
        }

        List<ApprovalDiffLine> result = [];
        int x = 0, y = 0;
        while (x < before.Count && y < after.Count)
        {
            if (before[x] == after[y])
            {
                result.Add(new ApprovalDiffLine(ApprovalDiffKind.Unchanged, before[x]));
                x++;
                y++;
            }
            else if (lcs[x + 1][y] >= lcs[x][y + 1])
            {
                result.Add(new ApprovalDiffLine(ApprovalDiffKind.Removed, before[x]));
                x++;
            }
            else
            {
                result.Add(new ApprovalDiffLine(ApprovalDiffKind.Added, after[y]));
                y++;
            }
        }

        while (x < before.Count)
            result.Add(new ApprovalDiffLine(ApprovalDiffKind.Removed, before[x++]));
        while (y < after.Count)
            result.Add(new ApprovalDiffLine(ApprovalDiffKind.Added, after[y++]));

        return result;
    }
}
