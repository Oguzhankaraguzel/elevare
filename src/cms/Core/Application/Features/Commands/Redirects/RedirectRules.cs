using Application.Abstraction.Data;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Redirects;

/// <summary>
/// The checks a hand-written redirect rule has to pass. Shared by create and edit so
/// the two cannot drift — an edit that skipped a check the create enforced would let
/// a user reach an invalid state by saving twice.
/// </summary>
public static class RedirectRules
{
    /// <summary>
    /// How far a proposed rule's chain is followed looking for a cycle. A loop is
    /// the one mistake here with no symptom in the CMS and a total outage on the
    /// public side, so it is worth the extra reads at save time.
    /// </summary>
    private const int MaxChainWalk = 10;

    /// <param name="editingId">Id of the rule being edited, so it does not collide with itself.</param>
    public static async Task<Result> ValidateAsync(
        ICmsApplicationDbContext db, int? editingId, string oldPath, string? newPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oldPath))
            return Result.Failure(RedirectErrors.OldPathRequired);

        string oldKey = RedirectPaths.ForComparison(oldPath);
        string newKey = RedirectPaths.ForComparison(newPath);

        if (newPath is not null && string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(RedirectErrors.OldPathCannotMatchNewPath);

        bool duplicate = await db.Redirects
            .AnyAsync(r => r.OldPath == oldPath && (editingId == null || r.Id != editingId), cancellationToken);
        if (duplicate)
            return Result.Failure(RedirectErrors.OldPathAlreadyExists);

        // A rule whose source is a page that still resolves would shadow that page:
        // visitors could never reach it again, and nothing on the Pages screen would
        // explain why.
        bool shadowsLivePage = await db.PageInfos
            .AnyAsync(p => p.FullSlug == oldPath && p.PageStatus == Domain.Entities.PageInfos.PageStatus.Published
                && p.Language.IsActive && p.Language.IsPublished, cancellationToken);
        if (shadowsLivePage)
            return Result.Failure(RedirectErrors.ShadowsLivePage);

        if (newPath is null || RedirectPaths.IsExternal(newPath))
            return Result.Success();

        return await WouldLoopAsync(db, editingId, oldKey, newKey, cancellationToken)
            ? Result.Failure(RedirectErrors.CircularRedirect)
            : Result.Success();
    }

    /// <summary>
    /// Follows the proposed target through the existing rules. If the walk arrives
    /// back at the path being redirected FROM, saving would strand every visitor who
    /// ever lands on it.
    /// </summary>
    private static async Task<bool> WouldLoopAsync(
        ICmsApplicationDbContext db, int? editingId, string oldKey, string startKey, CancellationToken cancellationToken)
    {
        List<(string Old, string? New)> rules = [.. (await db.Redirects
            .AsNoTracking()
            .Where(r => editingId == null || r.Id != editingId)
            .Select(r => new { r.OldPath, r.NewPath })
            .ToListAsync(cancellationToken))
            .Select(r => (RedirectPaths.ForComparison(r.OldPath), (string?)RedirectPaths.ForComparison(r.NewPath)))];

        var byOld = rules
            .GroupBy(r => r.Old, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().New, StringComparer.OrdinalIgnoreCase);

        string current = startKey;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { oldKey };

        for (int i = 0; i < MaxChainWalk; i++)
        {
            if (string.IsNullOrEmpty(current))
                return false;

            if (!seen.Add(current))
                return true;

            if (!byOld.TryGetValue(current, out string? next) || string.IsNullOrEmpty(next))
                return false;

            current = next;
        }

        // Too deep to resolve is treated as a loop: nothing that follows redirects
        // would reach the end of it anyway.
        return true;
    }
}
