using Application.Abstraction.Data;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Pages;

/// <summary>
/// Creates/updates the <see cref="Redirect"/> row that keeps a page's old public URL
/// from 404ing after its slug changes, or after the page itself is deleted/archived.
/// Used by <see cref="UpdatePage.UpdatePageCommandHandler"/>, <see cref="PageHierarchy.RecomputeDescendantSlugsAsync"/>,
/// and the Pages list's delete/status-change flows.
/// </summary>
public static class RedirectResolution
{
    /// <summary>
    /// Called when a Published page's slug changes. Every address the page has had
    /// keeps a rule of its own: each is bound to the page, and a bound rule resolves
    /// to wherever the page is now, so however often it moves, no old address is
    /// ever more than one hop away. This used to keep a single rule per page and
    /// move it to the latest old address — which quietly turned every earlier
    /// address into a 404 the moment the page moved a second time (a new default
    /// language moves every page at once).
    /// </summary>
    public static async Task<Result> UpsertForSlugChangeAsync(
        ICmsApplicationDbContext db, int pageId, string oldFullSlug, string newFullSlug, CancellationToken cancellationToken)
    {
        if (string.Equals(oldFullSlug, newFullSlug, StringComparison.Ordinal))
            return Result.Success();

        // Moving back to an address it had before: the page answers there itself again.
        Redirect? back = await db.Redirects
            .FirstOrDefaultAsync(r => r.OldPath == newFullSlug && r.SourcePageId == pageId, cancellationToken);
        if (back is not null)
            db.Redirects.Remove(back);

        // Every older address of the page names where it is now. A bound rule follows
        // the page by itself while it is live; this stored target is what it falls
        // back on once the page is unpublished — one hop to the page's last address,
        // whose own rule then says where visitors go instead.
        List<Redirect> bound = await db.Redirects
            .Where(r => r.SourcePageId == pageId && r.OldPath != newFullSlug)
            .ToListAsync(cancellationToken);
        foreach (Redirect rule in bound)
            rule.NewPath = newFullSlug;

        Redirect? existing = bound.Find(r => r.OldPath == oldFullSlug);
        if (existing is not null)
        {
            existing.Reason = RedirectReason.SlugChanged;
            return Result.Success();
        }

        // The rule just dropped would otherwise read as the other half of a loop.
        Result guard = await ValidateNewOldPathAsync(db, oldFullSlug, newFullSlug, cancellationToken, ignoreId: back?.Id);
        if (guard.IsFailure) return guard;

        db.Redirects.Add(new Redirect
        {
            OldPath = oldFullSlug,
            NewPath = newFullSlug,
            SourcePageId = pageId,
            Reason = RedirectReason.SlugChanged,
        });
        return Result.Success();
    }

    /// <summary>
    /// Called when a Published page is deleted or archived. <paramref name="targetUrl"/>
    /// is whatever the editor typed in the redirect prompt — null means no redirect
    /// (visitors just get a 404, today's behavior).
    /// </summary>
    // targetUrl is intentionally string, not Uri: it may be either a relative internal
    // path or an absolute external URL (same rationale as UpdatePageSeoMeta's CanonicalUrl/OgUrl).
#pragma warning disable CA1054
    public static async Task<Result> UpsertForPageRemovedAsync(
        ICmsApplicationDbContext db, int pageId, string fullSlug, string? targetUrl, RedirectReason reason, CancellationToken cancellationToken)
#pragma warning restore CA1054
    {
        // The rule on the page's current address — not just any rule bound to the page:
        // those are its earlier addresses, and moving one here would turn that
        // address into a 404. They keep pointing at this address, so they end up
        // wherever this rule sends visitors.
        Redirect? existing = await db.Redirects
            .FirstOrDefaultAsync(r => r.OldPath == fullSlug, cancellationToken);

        if (existing is not null)
        {
            existing.NewPath = targetUrl;
            existing.SourcePageId = null; // the page is gone/hidden now — nothing left to self-heal against
            existing.Reason = reason;
            return Result.Success();
        }

        if (targetUrl is not null)
        {
            Result guard = await ValidateNewOldPathAsync(db, fullSlug, targetUrl, cancellationToken);
            if (guard.IsFailure) return guard;
        }
        else if (await db.Redirects.AnyAsync(r => r.OldPath == fullSlug, cancellationToken))
        {
            return Result.Failure(RedirectErrors.OldPathAlreadyExists);
        }

        db.Redirects.Add(new Redirect
        {
            OldPath = fullSlug,
            NewPath = targetUrl,
            SourcePageId = null,
            Reason = reason,
        });
        return Result.Success();
    }

    /// <summary>
    /// Called when a soft-deleted page is restored from the Trash, with the page's
    /// FINAL (post-restore) FullSlug already computed. Only removes a redirect that
    /// exactly matches that slug — if the page came back under a different slug
    /// (an override, because the original was taken), any redirect still sitting at
    /// the OLD slug is left alone since it may now serve a different purpose entirely.
    /// </summary>
    public static async Task RemoveForRestoredPageAsync(ICmsApplicationDbContext db, string restoredFullSlug, CancellationToken cancellationToken)
    {
        Redirect? existing = await db.Redirects
            .FirstOrDefaultAsync(r => r.OldPath == restoredFullSlug, cancellationToken);

        if (existing is not null)
            db.Redirects.Remove(existing);
    }

    private static async Task<Result> ValidateNewOldPathAsync(
        ICmsApplicationDbContext db, string oldPath, string newPath, CancellationToken cancellationToken, int? ignoreId = null)
    {
        if (string.Equals(oldPath, newPath, StringComparison.Ordinal))
            return Result.Failure(RedirectErrors.OldPathCannotMatchNewPath);

        if (await db.Redirects.AnyAsync(r => r.OldPath == oldPath, cancellationToken))
            return Result.Failure(RedirectErrors.OldPathAlreadyExists);

        // Direct 2-hop cycle guard: someone already redirects newPath -> oldPath.
        if (await db.Redirects.AnyAsync(r => r.OldPath == newPath && r.NewPath == oldPath && r.Id != ignoreId, cancellationToken))
            return Result.Failure(RedirectErrors.CircularRedirect);

        return Result.Success();
    }
}
