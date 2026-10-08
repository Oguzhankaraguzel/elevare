using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Redirects.GetRedirects;

internal sealed class GetRedirectsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetRedirectsQuery, List<RedirectListItemResponse>>
{
    /// <summary>
    /// How far a chain is walked before it is called a loop. Well past anything a
    /// site legitimately needs — browsers give up around 20 hops and search engines
    /// far sooner — and it is what stops a genuine cycle from spinning forever.
    /// </summary>
    private const int MaxChainWalk = 10;

    public async Task<Result<List<RedirectListItemResponse>>> Handle(
        GetRedirectsQuery request, CancellationToken cancellationToken)
    {
        List<RedirectRow> rows = await db.Redirects
            .AsNoTracking()
            .Select(r => new RedirectRow(
                r.Id,
                r.OldPath,
                r.NewPath,
                r.SourcePageId,
                // The bound page's CURRENT slug, which is the whole point of binding:
                // a page renamed again must not leave this rule pointing at the old name.
                r.SourcePage == null || r.SourcePage.PageStatus != PageStatus.Published
                    ? null
                    : r.SourcePage.FullSlug,
                r.SourcePage == null ? null : r.SourcePage.SeoMeta.Title,
                r.Reason,
                r.CreateDate,
                r.UpdateDate))
            .ToListAsync(cancellationToken);

        // Every path a visitor could legitimately land on. Used to tell "redirects
        // somewhere real" from "redirects into a 404", which is the single most
        // useful thing this screen can say.
        HashSet<string> livePaths = [.. await db.PageInfos
            .AsNoTracking()
            // A page of a language that is off the site answers 404 like any other
            // missing page — which is exactly what its own temporary redirect is for.
            .Where(p => p.PageStatus == PageStatus.Published && p.IsActive
                && p.Language.IsActive && p.Language.IsPublished)
            .Select(p => p.FullSlug)
            .ToListAsync(cancellationToken)];

        var byOldPath = rows
            .GroupBy(r => Normalize(r.OldPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        List<RedirectListItemResponse> items = [.. rows
            .Select(r => Describe(r, byOldPath, livePaths))
            .OrderBy(i => i.OldPath, StringComparer.OrdinalIgnoreCase)];

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim();
            items = [.. items.Where(i =>
                i.OldPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (i.NewPath?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.SourcePageTitle?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))];
        }

        if (request.Reason is RedirectReason reason)
            items = [.. items.Where(i => i.Reason == reason)];

        if (request.RequiredHealth != RedirectHealth.None)
            items = [.. items.Where(i => (i.Health & request.RequiredHealth) == request.RequiredHealth)];

        return Result.Success(items);
    }

    private static RedirectListItemResponse Describe(
        RedirectRow row, Dictionary<string, RedirectRow> byOldPath, HashSet<string> livePaths)
    {
        // A bound page wins over the stored snapshot; that is what keeps a
        // twice-renamed page reachable through a single hop.
        string? target = row.LivePageSlug ?? row.NewPath;

        RedirectHealth health = RedirectHealth.None;
        int chainLength = 0;

        // Only a missing target is "Gone". An empty one is the default language's
        // homepage — its FullSlug is "" — and the public site resolves it as "/";
        // reading it as Gone flagged every rule pointing home as a 410.
        if (target is null)
        {
            health |= RedirectHealth.Gone;
        }
        else if (IsExternal(target))
        {
            // Another host's business. Calling it broken would be a lie, and calling
            // it healthy would overstate what we actually checked.
            health |= RedirectHealth.External;
        }
        else
        {
            (chainLength, bool looped) = WalkChain(row, byOldPath);
            if (looped)
                health |= RedirectHealth.Loop;
            else if (chainLength > 1)
                health |= RedirectHealth.Chained;

            // Judge the END of the chain, not the first hop: a rule pointing at
            // another rule is chained, but it is only broken if the final landing
            // place does not exist.
            string finalTarget = FinalTarget(row, byOldPath);
            if (!looped && !livePaths.Contains(Normalize(finalTarget)))
                health |= RedirectHealth.BrokenTarget;
        }

        return new RedirectListItemResponse(
            row.Id,
            row.OldPath,
            row.NewPath,
            target,
            row.SourcePageId,
            row.SourcePageTitle,
            row.Reason,
            health,
            chainLength,
            row.CreateDate,
            row.UpdateDate);
    }

    /// <summary>Number of hops a visitor makes, and whether the walk came back on itself.</summary>
    private static (int Hops, bool Looped) WalkChain(RedirectRow start, Dictionary<string, RedirectRow> byOldPath)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { Normalize(start.OldPath) };
        RedirectRow current = start;
        int hops = 0;

        for (int i = 0; i < MaxChainWalk; i++)
        {
            string? target = current.LivePageSlug ?? current.NewPath;
            if (string.IsNullOrWhiteSpace(target) || IsExternal(target))
                return (hops + 1, false);

            hops++;
            string key = Normalize(target);

            if (!seen.Add(key))
                return (hops, true);

            if (!byOldPath.TryGetValue(key, out RedirectRow? next))
                return (hops, false);

            current = next;
        }

        // Ran out of budget without closing the cycle: still a loop for every
        // practical purpose, since no client follows this many hops.
        return (hops, true);
    }

    private static string FinalTarget(RedirectRow start, Dictionary<string, RedirectRow> byOldPath)
    {
        RedirectRow current = start;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { Normalize(start.OldPath) };

        for (int i = 0; i < MaxChainWalk; i++)
        {
            string target = current.LivePageSlug ?? current.NewPath ?? string.Empty;
            string key = Normalize(target);

            if (!seen.Add(key) || !byOldPath.TryGetValue(key, out RedirectRow? next))
                return target;

            current = next;
        }

        return current.LivePageSlug ?? current.NewPath ?? string.Empty;
    }

    /// <summary>
    /// Paths are stored inconsistently by design — <c>OldPath</c> is FullSlug form
    /// with no leading slash, while a hand-typed <c>NewPath</c> usually has one.
    /// Comparing them raw is why a correct rule can look broken.
    /// </summary>
    private static string Normalize(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().TrimStart('/').TrimEnd('/');

    private static bool IsExternal(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("//", StringComparison.Ordinal);

    private sealed record RedirectRow(
        int Id,
        string OldPath,
        string? NewPath,
        int? SourcePageId,
        string? LivePageSlug,
        string? SourcePageTitle,
        RedirectReason Reason,
        DateTime CreateDate,
        DateTime? UpdateDate);
}
