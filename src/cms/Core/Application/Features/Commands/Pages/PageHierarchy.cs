using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Commands.Pages;

/// <summary>
/// Shared parent/child helpers for the page tree (category pages up to 3 levels
/// deep: e.g. "hekimler" → "hekimler/sami-sokucu"). Used by both create/update
/// commands so the depth and cycle rules stay identical everywhere a page's
/// parent can be set.
/// </summary>
public static class PageHierarchy
{
    /// <summary>Maximum allowed nesting level (1 = top-level, up to this value).</summary>
    public const int MaxDepth = 3;

    /// <summary>1 for a top-level page, 2 for its children, 3 for grandchildren, etc.</summary>
    public static async Task<int> GetLevelAsync(ICmsApplicationDbContext db, int pageId, CancellationToken cancellationToken)
    {
        int level = 1;
        int? currentParentId = await db.PageInfos
            .Where(p => p.Id == pageId)
            .Select(p => p.ParentPageId)
            .FirstOrDefaultAsync(cancellationToken);

        // The safety cap guards against bad data forming an accidental cycle;
        // MaxDepth itself is enforced by the callers before a page is ever saved.
        while (currentParentId is not null && level <= MaxDepth + 5)
        {
            level++;
            currentParentId = await db.PageInfos
                .Where(p => p.Id == currentParentId)
                .Select(p => p.ParentPageId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return level;
    }

    /// <summary>How many extra levels a page's deepest descendant adds (0 = no children).</summary>
    public static async Task<int> GetSubtreeExtraLevelsAsync(ICmsApplicationDbContext db, int pageId, CancellationToken cancellationToken)
    {
        int maxExtra = 0;
        Queue<(int Id, int Extra)> queue = new();
        queue.Enqueue((pageId, 0));
        HashSet<int> visited = [pageId];

        while (queue.Count > 0)
        {
            (int id, int extra) = queue.Dequeue();
            List<int> childIds = await db.PageInfos
                .Where(p => p.ParentPageId == id)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (int childId in childIds)
            {
                if (!visited.Add(childId))
                    continue;

                int childExtra = extra + 1;
                if (childExtra > maxExtra) maxExtra = childExtra;
                queue.Enqueue((childId, childExtra));
            }
        }

        return maxExtra;
    }

    /// <summary>True when <paramref name="candidateId"/> is <paramref name="pageId"/> itself or one of its descendants (i.e. setting it as the parent would create a cycle).</summary>
    public static async Task<bool> IsSelfOrDescendantAsync(ICmsApplicationDbContext db, int pageId, int candidateId, CancellationToken cancellationToken)
    {
        if (pageId == candidateId)
            return true;

        Queue<int> queue = new();
        queue.Enqueue(pageId);
        HashSet<int> visited = [pageId];

        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            List<int> childIds = await db.PageInfos
                .Where(p => p.ParentPageId == id)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (int childId in childIds)
            {
                if (childId == candidateId) return true;
                if (visited.Add(childId)) queue.Enqueue(childId);
            }
        }

        return false;
    }

    /// <summary>
    /// Recomputes <see cref="PageInfo.FullSlug"/> for every descendant of <paramref name="page"/>,
    /// since it's denormalized/stored per page — a parent's slug or parent change would
    /// otherwise leave descendants pointing at a stale URL.
    /// </summary>
    /// <param name="moves">When given, collects every changed FullSlug (old → new), drafts included.</param>
    public static async Task RecomputeDescendantSlugsAsync(
        ICmsApplicationDbContext db, PageInfo page, string defaultLanguageCode, CancellationToken cancellationToken,
        Dictionary<string, string>? moves = null)
    {
        List<PageInfo> children = await db.PageInfos
            .Where(p => p.ParentPageId == page.Id)
            .ToListAsync(cancellationToken);

        foreach (PageInfo child in children)
        {
            string oldFullSlug = child.FullSlug;
            child.ParentPage = page;
            child.ComputeFullSlug(defaultLanguageCode);

            if (child.FullSlug != oldFullSlug)
                moves?.TryAdd(oldFullSlug, child.FullSlug);
            if (child.PageStatus == PageStatus.Published && child.FullSlug != oldFullSlug)
                await RedirectResolution.UpsertForSlugChangeAsync(db, child.Id, oldFullSlug, child.FullSlug, cancellationToken);

            await RecomputeDescendantSlugsAsync(db, child, defaultLanguageCode, cancellationToken, moves);
        }
    }

    /// <summary>
    /// Recomputes <see cref="PageInfo.FullSlug"/> for every page on the site, top level
    /// down. <see cref="PageInfo.ComputeFullSlug"/> omits the language prefix only for
    /// whichever language is currently default — so the moment that flag moves to a
    /// different language, EVERY page's stored FullSlug disagrees with what the public
    /// site computes live at request time (see <c>GetPublicPageBySlugQueryHandler</c>,
    /// which reconstructs the same key from the *current* default) until this runs.
    /// Left undone, that mismatch 404s the entire site the moment the switch is saved.
    /// Reuses <see cref="RecomputeDescendantSlugsAsync"/>'s redirect safety net, so a
    /// bookmarked pre-switch URL still resolves afterwards.
    /// </summary>
    /// <returns>Every changed FullSlug, old → new.</returns>
    public static async Task<Dictionary<string, string>> RecomputeAllSlugsForDefaultLanguageChangeAsync(
        ICmsApplicationDbContext db, string newDefaultLanguageCode, CancellationToken cancellationToken)
    {
        List<PageInfo> topLevelPages = await db.PageInfos
            .Include(p => p.Language)
            .Where(p => p.ParentPageId == null)
            .ToListAsync(cancellationToken);

        Dictionary<string, string> moves = new(StringComparer.Ordinal);
        foreach (PageInfo page in topLevelPages)
        {
            string oldFullSlug = page.FullSlug;
            page.ComputeFullSlug(newDefaultLanguageCode);

            if (page.FullSlug != oldFullSlug)
                moves.TryAdd(oldFullSlug, page.FullSlug);
            if (page.PageStatus == PageStatus.Published && page.FullSlug != oldFullSlug)
                await RedirectResolution.UpsertForSlugChangeAsync(db, page.Id, oldFullSlug, page.FullSlug, cancellationToken);

            await RecomputeDescendantSlugsAsync(db, page, newDefaultLanguageCode, cancellationToken, moves);
        }
        return moves;
    }
}
