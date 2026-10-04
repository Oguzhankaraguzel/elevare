using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// The site codes one page has switched off (<c>PageInfoSiteCodeExclusions</c>).
/// Read per request, not cached: it is one indexed lookup by page id, and a page
/// whose author has just turned the chat widget off should not keep serving it
/// for another half minute.
/// </summary>
public static class SiteCodeExclusions
{
    public const string ViewDataKey = "ExcludedSiteCodeIds";

    public static async Task<HashSet<int>> ForPageAsync(IPublicReadDbContext db, int pageId, CancellationToken cancellationToken)
    {
        List<int> ids = await db.PageInfoSiteCodeExclusions
            .Where(x => x.PageInfoId == pageId)
            .Select(x => x.SiteCodeSnippetId)
            .ToListAsync(cancellationToken);
        return [.. ids];
    }
}
