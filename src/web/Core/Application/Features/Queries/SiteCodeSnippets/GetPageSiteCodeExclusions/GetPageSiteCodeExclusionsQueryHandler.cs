using Application.Abstraction.Data;
using Application.Services;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteCodeSnippets.GetPageSiteCodeExclusions;

internal sealed class GetPageSiteCodeExclusionsQueryHandler(IPublicReadDbContext db)
    : IQueryHandler<GetPageSiteCodeExclusionsQuery, HashSet<int>>
{
    public async Task<Result<HashSet<int>>> Handle(GetPageSiteCodeExclusionsQuery request, CancellationToken cancellationToken) =>
        Result.Success(await SiteCodeExclusions.ForPageAsync(db, request.PageId, cancellationToken));
}
