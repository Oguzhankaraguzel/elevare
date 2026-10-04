using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteCodeSnippets.GetSiteCodeSnippets;

internal sealed class GetSiteCodeSnippetsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetSiteCodeSnippetsQuery, List<SiteCodeSnippetResponse>>
{
    public async Task<Result<List<SiteCodeSnippetResponse>>> Handle(
        GetSiteCodeSnippetsQuery request, CancellationToken cancellationToken)
    {
        List<SiteCodeSnippetResponse> snippets = await db.SiteCodeSnippets
            .AsNoTracking()
            // Placement first, then SortOrder: the same comparison the Web layout
            // makes, so what the CMS lists top-to-bottom is what the page emits.
            .OrderBy(s => s.Placement).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new SiteCodeSnippetResponse(
                s.Id,
                s.Name,
                s.Preset,
                s.Placement,
                s.Kind,
                s.Content,
                s.IsEnabled,
                s.SortOrder,
                s.Notes,
                s.UpdateDate))
            .ToListAsync(cancellationToken);

        return Result.Success(snippets);
    }
}
