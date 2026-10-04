using Application.Abstraction.Data;
using Domain.Entities.PageContents;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.ContentBulkEdits.SearchPagesByContent;

internal sealed class SearchPagesByContentQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<SearchPagesByContentQuery, List<PageContentMatchResponse>>
{
    public async Task<Result<List<PageContentMatchResponse>>> Handle(
        SearchPagesByContentQuery request,
        CancellationToken cancellationToken)
    {
        // Deliberately NOT case-insensitive, unlike every other search in the CMS.
        // This one previews a find-and-replace, and the replace runs through
        // string.Replace — ordinal. Widening the search here would list pages whose
        // text the apply step then leaves untouched, which reads as the feature
        // silently failing.
        string search = request.SearchText;
        if (string.IsNullOrEmpty(search))
            return Result.Success(new List<PageContentMatchResponse>());

        List<PageContent> matches = await db.PageContents
            .AsNoTracking()
            .Include(c => c.PageInfo).ThenInclude(p => p.Language)
            .Where(c => !c.PageInfo.IsDeleted &&
                (c.GjsHtml != null && c.GjsHtml.Contains(search) ||
                 c.GjsCss != null && c.GjsCss.Contains(search) ||
                 c.GjsData != null && c.GjsData.Contains(search)))
            .ToListAsync(cancellationToken);

        // The SQL filter above runs under the database's default (case/accent
        // -insensitive) collation, but ApplyContentBulkEditCommandHandler replaces
        // with an ordinal, exact-case match — the only kind that can't silently
        // corrupt unrelated text. A page whose only occurrence differs by case
        // (the DB call finds it, Ordinal doesn't) would otherwise show up here
        // pre-selected with "0 eşleşme": confusing, and if the user trusts the
        // checkbox over the count, a click on "Tümünü seç" → Güncelle would look
        // like it updated a page that Apply then silently skips.
        List<PageContentMatchResponse> result = [.. matches
            .Select(c => new PageContentMatchResponse(
                c.PageInfoId,
                c.PageInfo.SeoMeta.Title,
                c.PageInfo.FullSlug,
                c.PageInfo.Language.NameInNative,
                CountOccurrences(c.GjsHtml, search) + CountOccurrences(c.GjsCss, search) + CountOccurrences(c.GjsData, search)))
            .Where(m => m.MatchCount > 0)
            .OrderBy(m => m.FullSlug)];

        return Result.Success(result);
    }

    private static int CountOccurrences(string? haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack)) return 0;

        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
