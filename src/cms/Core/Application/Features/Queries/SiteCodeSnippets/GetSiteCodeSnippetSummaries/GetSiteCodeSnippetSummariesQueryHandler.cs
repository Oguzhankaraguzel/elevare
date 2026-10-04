using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteCodeSnippets.GetSiteCodeSnippetSummaries;

internal sealed class GetSiteCodeSnippetSummariesQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetSiteCodeSnippetSummariesQuery, List<SiteCodeSnippetSummaryResponse>>
{
    public async Task<Result<List<SiteCodeSnippetSummaryResponse>>> Handle(
        GetSiteCodeSnippetSummariesQuery request, CancellationToken cancellationToken)
    {
        List<SiteCodeSnippetSummaryResponse> snippets = await db.SiteCodeSnippets
            .AsNoTracking()
            .OrderBy(s => s.Placement).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new SiteCodeSnippetSummaryResponse(s.Id, s.Name, s.Preset, s.Placement, s.Kind, s.IsEnabled))
            .ToListAsync(cancellationToken);

        return Result.Success(snippets);
    }
}
