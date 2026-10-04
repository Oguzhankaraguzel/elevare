using System.Data.Common;
using System.Text;
using AngleSharp.Dom;
using AngleSharp;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Services;

/// <summary>
/// Resolves "linked template" reference blocks — <c>&lt;div class="elevare-tpl-ref"
/// data-elevare-template-id="ID"&gt;</c> — embedded in GrapeJS-authored HTML to each
/// template's CURRENT content, at request time. This is what makes editing a linked
/// template propagate to every page that uses it, with no caching or explicit
/// re-save/propagation step: every request re-resolves against the live template row.
/// </summary>
public sealed class TemplateResolutionService(IPublicReadDbContext db)
{
    private const string MarkerClass = "elevare-tpl-ref";
    // Unlinked copies carry their content already, so nothing resolves them — but
    // they are the same kind of wrapper and cause the same layout problem below.
    private const string SnapshotClass = "elevare-tpl-snapshot";
    private const string MarkerAttribute = "data-elevare-template-id";
    private const int MaxNestingDepth = 5;

    /// <summary>Returns the HTML with every linked-template marker replaced by its current
    /// content, plus the accumulated CSS of the resolved templates.
    /// <para>
    /// Returns a <see cref="Result"/> because this runs inside the request that is
    /// rendering a page: it reads the database, so it can fail, and the caller needs
    /// to decide between showing the page unresolved and showing an error page. A
    /// thrown exception took that decision away.
    /// </para>
    /// </summary>
    public async Task<Result<ResolvedTemplateContent>> ResolveAsync(string? html, CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveCoreAsync(html, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure<ResolvedTemplateContent>(
                RenderErrors.ResolutionFailed("linked templates", ex.Message));
        }
    }

    private async Task<Result<ResolvedTemplateContent>> ResolveCoreAsync(string? html, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
            return Result.Success(new ResolvedTemplateContent(html, string.Empty));

        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        using IDocument document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        List<IElement> markers = [.. document.QuerySelectorAll('.' + MarkerClass)];
        if (markers.Count == 0)
            // Still return the parsed body's inner HTML: GrapeJS exports often wrap
            // content in a <body> tag, which would otherwise be emitted nested
            // inside the layout's own <body>.
        {
            UnwrapTemplateWrappers(document);
            return Result.Success(new ResolvedTemplateContent(document.Body?.InnerHtml ?? html, string.Empty));
        }

        // In passes: a linked template can hold another one (a menu with a linked
        // search box), and that inner marker only appears once the outer one has
        // been filled — carrying whatever copy of the inner template was saved with
        // it, not its current content. Each pass resolves the markers the previous
        // one brought in. A template that turns up inside itself is left as it is
        // rather than nested again, and the depth is capped either way.
        var cssBuilder = new StringBuilder();
        var resolved = new HashSet<IElement>();
        var cssAdded = new HashSet<int>();
        for (int pass = 0; pass < MaxNestingDepth && markers.Count > 0; pass++)
        {
            int[] ids = [.. markers
                .Select(m => int.TryParse(m.GetAttribute(MarkerAttribute), out int parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()];

            Dictionary<int, PublicPageTemplate> templates = await db.PageTemplates
                .Where(t => ids.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken);

            foreach (IElement marker in markers)
            {
                resolved.Add(marker);
                string? idText = marker.GetAttribute(MarkerAttribute);
                if (idText is null
                    || !int.TryParse(idText, out int id)
                    || !templates.TryGetValue(id, out PublicPageTemplate? template)
                    || InsideItself(marker, idText))
                    continue;

                marker.InnerHtml = template.GjsHtml ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(template.GjsCss) && cssAdded.Add(id))
                    cssBuilder.Append(template.GjsCss);
            }

            markers = [.. document.QuerySelectorAll('.' + MarkerClass).Where(m => !resolved.Contains(m))];
        }

        UnwrapTemplateWrappers(document);
        // The page's export already carries scripts for the template's components
        // (its locked copy); the template just brought its own. One binding each.
        ComponentScriptDeduplicator.Apply(document);

        return Result.Success(new ResolvedTemplateContent(document.Body?.InnerHtml ?? html, cssBuilder.ToString()));
    }

    /// <summary>
    /// Removes the template wrappers themselves, leaving their contents in place.
    /// <para>
    /// The wrapper is an editor construct: inside the page builder it is the thing
    /// you select, move and delete to manage a linked template. Once the template has
    /// been substituted in, it has no job left — it is a bare
    /// <c>&lt;div class="elevare-tpl-ref" data-elevare-template-id="1"&gt;</c> sitting
    /// in the published markup, wrapping content that should simply be part of the
    /// page. Nothing on the public side reads it (checked: only this service does).
    /// </para>
    /// <para>
    /// This replaces an earlier attempt that left the div in place and neutralised it
    /// with <c>display: contents</c>. That fixed the layout symptom — a wrapper box
    /// breaking <c>position: sticky</c> inside a header — but kept the element, and
    /// with it a stray div in everyone's page source. Removing the wrapper outright
    /// fixes both, and needs no CSS at all.
    /// </para>
    /// </summary>
    private static bool InsideItself(IElement marker, string idText)
    {
        for (IElement? parent = marker.ParentElement; parent is not null; parent = parent.ParentElement)
        {
            if (parent.ClassList.Contains(MarkerClass) && parent.GetAttribute(MarkerAttribute) == idText)
                return true;
        }
        return false;
    }

    private static void UnwrapTemplateWrappers(IDocument document)
    {
        foreach (IElement wrapper in document.QuerySelectorAll($".{MarkerClass}, .{SnapshotClass}"))
        {
            INode? parent = wrapper.Parent;
            if (parent is null)
                continue;

            // Moved one at a time rather than through Replace(): the child list is
            // live, so taking the first child repeatedly is what actually drains it.
            while (wrapper.FirstChild is INode child)
                parent.InsertBefore(child, wrapper);

            wrapper.Remove();
        }
    }
}
