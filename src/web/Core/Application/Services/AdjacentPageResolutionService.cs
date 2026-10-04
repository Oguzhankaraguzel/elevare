using System.Data.Common;
using AngleSharp;
using AngleSharp.Dom;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Services;

/// <summary>
/// Fills the "Önceki / Sonraki Yazı" block: the pages published just before and just
/// after this one under the same parent — an article's neighbours in its category —
/// by publish date, the same order the listing's "newest" uses. A side with no
/// neighbour is left empty (so the other keeps its place); a page with neither loses
/// the block.
/// </summary>
public sealed class AdjacentPageResolutionService(IPublicReadDbContext db)
{
    private const string MarkerAttribute = "data-elevare-adjacent";

    public async Task<Result<string?>> ResolveAsync(string? html, int currentPageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains(MarkerAttribute, StringComparison.Ordinal))
            return Result.Success(html);
        try
        {
            return Result.Success(await ResolveCoreAsync(html, currentPageId, cancellationToken));
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure<string?>(RenderErrors.ResolutionFailed("the previous/next block", ex.Message));
        }
    }

    private async Task<string?> ResolveCoreAsync(string html, int currentPageId, CancellationToken cancellationToken)
    {
        using IDocument document = await BrowsingContext.New(Configuration.Default)
            .OpenAsync(req => req.Content(html), cancellationToken);

        var current = await db.PageInfos
            .Where(p => p.Id == currentPageId)
            .Select(p => new { p.Id, p.ParentPageId, p.LanguageId, Date = p.PublishedAt ?? p.CreateDate })
            .FirstOrDefaultAsync(cancellationToken);

        PublicPage? previous = null;
        PublicPage? next = null;
        // A top-level page has no category to walk through.
        if (current is { ParentPageId: not null })
        {
            IQueryable<PublicPage> siblings = db.PageInfos.AsNoTracking().Where(p =>
                p.ParentPageId == current.ParentPageId && p.LanguageId == current.LanguageId && p.Id != current.Id
                && p.PageStatus == PublicPageStatus.Published && p.IsActive);

            previous = await siblings
                .Where(p => (p.PublishedAt ?? p.CreateDate) < current.Date
                    || (p.PublishedAt ?? p.CreateDate) == current.Date && p.Id < current.Id)
                .OrderByDescending(p => p.PublishedAt ?? p.CreateDate).ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);
            next = await siblings
                .Where(p => (p.PublishedAt ?? p.CreateDate) > current.Date
                    || (p.PublishedAt ?? p.CreateDate) == current.Date && p.Id > current.Id)
                .OrderBy(p => p.PublishedAt ?? p.CreateDate).ThenBy(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        foreach (IElement link in document.QuerySelectorAll($"[{MarkerAttribute}]").ToList())
        {
            PublicPage? target = link.GetAttribute(MarkerAttribute) == "prev" ? previous : next;
            if (target is null)
            {
                // Keeps the other link on its own side of the row.
                IElement spacer = document.CreateElement("span");
                spacer.SetAttribute("aria-hidden", "true");
                link.Replace(spacer);
                continue;
            }
            link.SetAttribute("href", "/" + target.FullSlug);
            string title = string.IsNullOrWhiteSpace(target.SeoTitle) ? target.Slug : target.SeoTitle;
            foreach (IElement field in link.QuerySelectorAll("[data-elevare-field=title]"))
                field.TextContent = title;
        }

        if (previous is null && next is null)
        {
            foreach (IElement block in document.QuerySelectorAll("[data-elevare-block=elevare-prevnext]").ToList())
                block.Remove();
        }

        return document.Body?.InnerHtml ?? html;
    }
}
