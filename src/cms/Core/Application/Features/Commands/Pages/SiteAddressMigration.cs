using Application.Abstraction.Data;
using Application.Features.Commands.StructuredData;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.Redirects;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Commands.Pages;

/// <summary>
/// After pages have moved, points everything the CMS has stored at their new
/// addresses: links in page and template content (published and pending), each
/// page's og:url, canonical, structured data and social fields, site settings such
/// as llms.txt, and manual redirects that sent visitors to an old address.
/// <para>
/// The redirects left behind keep old links working, but a link that has to be
/// redirected on every click, an og:url that names a URL the page no longer
/// lives at, or a structured data <c>@id</c> that disagrees with the canonical are
/// all things search engines notice. Moving the pages back runs this the other way.
/// </para>
/// </summary>
public static class SiteAddressMigration
{
    /// <returns>How many records had at least one address rewritten.</returns>
    public static async Task<int> ApplyAsync(
        ICmsApplicationDbContext db, IReadOnlyDictionary<string, string> moves, CancellationToken cancellationToken)
    {
        string? baseUrl = await db.SiteSettings.AsNoTracking()
            .Where(s => s.Key == SchemaNodeFactory.PublicSiteBaseUrlKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? siteBaseUrl);
        var rewriter = SiteAddressRewriter.Create(moves, siteBaseUrl);
        if (rewriter is null)
            return 0;

        int changed = 0;
        int edits = 0;
        void Rewrite(string? value, Action<string?> set)
        {
            string? rewritten = rewriter.Rewrite(value);
            if (string.Equals(rewritten, value, StringComparison.Ordinal))
                return;
            set(rewritten);
            edits++;
        }
        // One record counts once, however many of its fields had addresses in them.
        void Count(int editsBefore)
        {
            if (edits > editsBefore) changed++;
        }

        foreach (PageContent c in await db.PageContents.ToListAsync(cancellationToken))
        {
            int before = edits;
            Rewrite(c.GjsHtml, v => c.GjsHtml = v);
            Rewrite(c.GjsCss, v => c.GjsCss = v);
            Rewrite(c.GjsData, v => c.GjsData = v);
            Rewrite(c.PreviewGjsHtml, v => c.PreviewGjsHtml = v);
            Rewrite(c.PreviewGjsCss, v => c.PreviewGjsCss = v);
            Count(before);
        }

        foreach (PageTemplate t in await db.PageTemplates.ToListAsync(cancellationToken))
        {
            int before = edits;
            Rewrite(t.GjsHtml, v => t.GjsHtml = v);
            Rewrite(t.GjsCss, v => t.GjsCss = v);
            Rewrite(t.GjsData, v => t.GjsData = v);
            Rewrite(t.PreviewGjsHtml, v => t.PreviewGjsHtml = v);
            Rewrite(t.PreviewGjsCss, v => t.PreviewGjsCss = v);
            Count(before);
        }

        foreach (PageInfo p in await db.PageInfos.ToListAsync(cancellationToken))
        {
            SeoMeta seo = p.SeoMeta;
            int before = edits;
            Rewrite(seo.OgUrl, v => seo.OgUrl = v);
            Rewrite(seo.CanonicalUrl, v => seo.CanonicalUrl = v);
            Rewrite(seo.StructuredData, v => seo.StructuredData = v);
            Rewrite(seo.SocialJson, v => seo.SocialJson = v);
            Count(before);
        }

        foreach (SiteSetting s in await db.SiteSettings.ToListAsync(cancellationToken))
        {
            int before = edits;
            Rewrite(s.Value, v => s.Value = v);
            Count(before);
        }

        // A redirect tied to a page follows that page by itself. One typed by hand
        // stores its target as a path ("en/about") or a full address.
        foreach (Redirect r in await db.Redirects.Where(r => r.SourcePageId == null && r.NewPath != null).ToListAsync(cancellationToken))
        {
            string target = r.NewPath!;
            // Kept in the shape it was typed in: "/en/about" stays root-relative.
            string? moved = rewriter.Rewrite(target);
            if (moves.TryGetValue(target.Trim('/'), out string? to))
                moved = target.StartsWith('/') ? "/" + to : to;
            if (moved is not null && !string.Equals(moved, target, StringComparison.Ordinal))
            {
                r.NewPath = moved;
                changed++;
            }
        }

        return changed;
    }
}
