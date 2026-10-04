using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Application.Abstraction.Data;
using Domain.Entities.SiteCodeSnippets;
using Microsoft.EntityFrameworkCore;

namespace Wasm.Endpoints;

/// <summary>
/// Serves the site's own Site Codes CSS (brand color variables, critical CSS, base
/// styles) back into the page builder's GrapesJS canvas iframe.
/// <para>
/// Without this, the canvas has no idea the <c>--elevare-*</c> custom properties
/// exist at all — it never loads Site Codes, only a couple of static assets (see
/// <c>grapes-editor.js</c>'s <c>canvas.styles</c>) — so every block that reads
/// <c>var(--elevare-primary, #2563eb)</c> silently falls back to its literal
/// default inside the editor, showing the wrong color even when the live site (via
/// <c>_Layout.cshtml</c>) is rendering the real one correctly. This endpoint is
/// added to that same <c>canvas.styles</c> array so what an editor sees matches
/// what a visitor sees.
/// </para>
/// </summary>
public static class CanvasPreviewEndpoints
{
    private static readonly Regex StyleTagPattern = new(
        "<style[^>]*>|</style>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A Style snippet is not always an inline <style> block — a CDN stylesheet
    // (Bootstrap Icons, the cookie-consent banner's CSS, a webfont…) is authored
    // as a bare <link rel="stylesheet" href="..."> instead, same as it would be
    // in a real <head>. That tag has no meaning inside a CSS document — a browser
    // parsing this response just discards it as invalid syntax — so it is
    // rewritten into the CSS-native equivalent, @import, which a stylesheet CAN
    // pull in another stylesheet with.
    private static readonly Regex LinkStylesheetPattern = new(
        """<link\b[^>]*\brel\s*=\s*["']stylesheet["'][^>]*\bhref\s*=\s*["'](?<href>[^"']+)["'][^>]*/?>""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static WebApplication MapCanvasPreviewEndpoints(this WebApplication app)
    {
        // ?exclude=3,7 — the site codes the page being edited has switched off
        // (PageInfoSiteCodeExclusions), so the canvas drops the same stylesheets the
        // live page will. Passed by the editor, not read from the page here: the
        // set changes while the page is open and is not saved until the page is.
        app.MapGet("/_canvas-preview.css", async (
            string? exclude, ICmsApplicationDbContext db, CancellationToken cancellationToken) =>
        {
            HashSet<int> excluded = ParseIds(exclude);
            List<string?> contents = await db.SiteCodeSnippets
                .AsNoTracking()
                .Where(s => s.Kind == SiteCodeKind.Style && s.IsEnabled && !excluded.Contains(s.Id))
                .OrderBy(s => s.Placement).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Select(s => s.Content)
                .ToListAsync(cancellationToken);

            // @import is only honoured by a browser at the very top of a stylesheet —
            // one appearing after an ordinary rule is dropped outright — so every
            // <link>-shaped row is collected separately and emitted first, ahead of
            // everything else, regardless of where it actually sits in Site Codes'
            // own Placement/SortOrder (which governs a real <head>, not a CSS file).
            StringBuilder imports = new();
            StringBuilder rules = new();
            foreach (string? content in contents)
            {
                if (string.IsNullOrWhiteSpace(content)) continue;

                // A snippet is not necessarily one or the other. An operator pasting
                // a webfont commonly writes the <link> AND a couple of rules that use
                // it in the same row, so the two halves are separated rather than the
                // row being classified: every <link> becomes an @import at the top,
                // and whatever is left goes in with the rules.
                //
                // Previously such a row was handled as "a link row" and returned
                // early, which lost its CSS entirely and — worse — appended the rest
                // of it, <style> tags included, into the imports block at the very top
                // of the response, where a browser stops parsing at the first
                // syntax error and takes every @import below it down too.
                string remainder = content;

                if (LinkStylesheetPattern.IsMatch(content))
                {
                    foreach (Match link in LinkStylesheetPattern.Matches(content))
                        imports.AppendLine(CultureInfo.InvariantCulture, $"""@import url("{link.Groups["href"].Value}");""");

                    remainder = LinkStylesheetPattern.Replace(content, "");
                }

                // Content is a full <style>...</style> element (authors write the tag
                // themselves — see SiteCodeSnippet.Content). A CSS response can't
                // contain the tag itself, only what was inside it.
                remainder = StyleTagPattern.Replace(remainder, "");
                if (!string.IsNullOrWhiteSpace(remainder))
                    rules.AppendLine(remainder);
            }

            return Results.Text(imports.Append(rules).ToString(), "text/css", Encoding.UTF8);
        })
        // Read into the editor's own iframe — no session, no sensitive data, same
        // trust level as the CSS a visitor's browser already downloads unauthenticated.
        .AllowAnonymous();

        return app;
    }

    private static HashSet<int> ParseIds(string? csv)
    {
        HashSet<int> ids = [];
        if (string.IsNullOrWhiteSpace(csv)) return ids;
        foreach (string part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) ids.Add(id);
        }
        return ids;
    }
}
