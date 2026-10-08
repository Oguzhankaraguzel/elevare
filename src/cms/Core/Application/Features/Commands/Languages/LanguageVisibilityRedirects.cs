using Application.Abstraction.Data;
using Domain.Entities.Languages;
using Domain.Entities.PageInfos;
using Domain.Entities.Redirects;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Commands.Languages;

/// <summary>
/// The temporary redirects that stand in for a language while it is off the site.
/// <para>
/// They are ordinary rows in the Redirects table, so the Redirects screen lists them
/// and an editor can change or remove any of them. <see cref="RedirectReason.LanguageUnpublished"/>
/// is what finds them again when the language comes back. While the language is
/// live they do nothing: the site only consults a redirect once the address has
/// failed to resolve to a page.
/// </para>
/// </summary>
public static class LanguageVisibilityRedirects
{
    /// <summary>
    /// One 302 rule per published page of <paramref name="language"/>, pointing at the
    /// page's default-language counterpart (followed by id, so a later rename of the
    /// counterpart does not leave the rule behind) or the default homepage. An address
    /// that already has a rule keeps it.
    /// </summary>
    public static async Task<int> CreateAsync(
        ICmsApplicationDbContext db, Language language, CancellationToken cancellationToken)
    {
        int? defaultLanguageId = await db.Languages
            .Where(l => l.IsDefault && l.Id != language.Id)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (defaultLanguageId is null)
            return 0;

        // 404/500/maintenance are what the site shows when something has gone wrong,
        // never an address anyone links to, so they need no stand-in.
        List<PageInfo> pages = await db.PageInfos
            .AsNoTracking()
            .Where(p => p.LanguageId == language.Id && p.PageStatus == PageStatus.Published
                && !SystemPageSlugs.All.Contains(p.Slug))
            .ToListAsync(cancellationToken);

        List<int> groupIds = [.. pages.Where(p => p.PageGroupId is not null).Select(p => p.PageGroupId!.Value).Distinct()];
        var counterparts = (await db.PageInfos
                .AsNoTracking()
                .Where(p => p.LanguageId == defaultLanguageId
                    && p.PageGroupId != null && groupIds.Contains(p.PageGroupId.Value)
                    && p.PageStatus == PageStatus.Published && p.IsActive)
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.PageGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        List<string> paths = [.. pages.Select(p => p.FullSlug)];
        HashSet<string> taken = new(
            await db.Redirects.Where(r => paths.Contains(r.OldPath)).Select(r => r.OldPath).ToListAsync(cancellationToken),
            StringComparer.Ordinal);

        int created = 0;
        foreach (PageInfo page in pages)
        {
            if (!taken.Add(page.FullSlug))
                continue;

            PageInfo? counterpart = page.PageGroupId is int group ? counterparts.GetValueOrDefault(group) : null;
            db.Redirects.Add(new Redirect
            {
                OldPath = page.FullSlug,
                SourcePageId = counterpart?.Id,
                // The frozen fallback, used only once the counterpart itself stops resolving.
                NewPath = counterpart is null ? "/" : "/" + counterpart.FullSlug,
                Reason = RedirectReason.LanguageUnpublished,
                IsTemporary = true,
            });
            created++;
        }

        return created;
    }

    /// <summary>The rules <see cref="CreateAsync"/> wrote for the language with this code.</summary>
    public static IQueryable<Redirect> For(ICmsApplicationDbContext db, string languageCode)
    {
        string prefix = languageCode + "/";
        return db.Redirects.Where(r => r.Reason == RedirectReason.LanguageUnpublished
            && (r.OldPath == languageCode || r.OldPath.StartsWith(prefix)));
    }

    public static async Task<int> RemoveAsync(
        ICmsApplicationDbContext db, string languageCode, CancellationToken cancellationToken)
    {
        List<Redirect> rules = await For(db, languageCode).ToListAsync(cancellationToken);
        foreach (Redirect rule in rules)
            db.Redirects.Remove(rule);
        return rules.Count;
    }
}
