using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Application.Features.Commands.Pages;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Commands.Languages;

/// <summary>
/// What making a language the default sets in motion, shared by creating and
/// updating one: every page's address is recomputed (the default language's pages
/// lose their prefix, every other one gains it), old addresses are redirected,
/// every stored link and address is pointed at the new ones, and the sitemaps are
/// rebuilt instead of serving the old addresses until the next scheduled run.
/// </summary>
internal static class DefaultLanguageSwitch
{
    public static async Task<LanguageSaveResult> ApplyAsync(
        ICmsApplicationDbContext db, ISitemapRegenerator sitemaps, string newDefaultCode, CancellationToken cancellationToken)
    {
        Dictionary<string, string> moves =
            await PageHierarchy.RecomputeAllSlugsForDefaultLanguageChangeAsync(db, newDefaultCode, cancellationToken);
        int updated = await SiteAddressMigration.ApplyAsync(db, await WithEarlierAddressesAsync(db, moves, cancellationToken), cancellationToken);
        sitemaps.RequestRegeneration();
        return new LanguageSaveResult(true, moves.Count, updated);
    }

    /// <summary>
    /// The moves plus every address a page had before an earlier rename, still
    /// redirected to it — a link written back then works only through that
    /// redirect, and points at the page's new address directly from now on. An old
    /// address that some page answers at today is left alone: the link means that page.
    /// </summary>
    private static async Task<Dictionary<string, string>> WithEarlierAddressesAsync(
        ICmsApplicationDbContext db, Dictionary<string, string> moves, CancellationToken cancellationToken)
    {
        // The recompute above loaded and changed every page; the database still has the old slugs.
        var current = db.PageInfos.Local.ToDictionary(p => p.Id, p => p.FullSlug);
        HashSet<string> taken = [.. current.Values];

        var earlier = await db.Redirects.AsNoTracking()
            .Where(r => r.SourcePageId != null)
            .Select(r => new { r.OldPath, PageId = r.SourcePageId!.Value })
            .ToListAsync(cancellationToken);

        Dictionary<string, string> addresses = new(moves, StringComparer.Ordinal);
        foreach (var r in earlier)
        {
            if (!taken.Contains(r.OldPath) && current.TryGetValue(r.PageId, out string? now))
                addresses.TryAdd(r.OldPath, now);
        }
        return addresses;
    }
}
