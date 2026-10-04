using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Tags.GetTagUsage;

internal sealed class GetTagUsageQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetTagUsageQuery, List<TagUsageResponse>>
{
    public async Task<Result<List<TagUsageResponse>>> Handle(
        GetTagUsageQuery request, CancellationToken cancellationToken)
    {
        List<TagUsageResponse> tags = await db.Tags
            .AsNoTracking()
            // Unused first: this screen exists to find the tags nobody meant to keep,
            // and an alphabetical list buries them among the ones in daily use.
            .OrderBy(t => t.PageInfos!.Count(p => !p.IsDeleted))
            .ThenBy(t => t.Name)
            .Select(t => new TagUsageResponse(
                t.Id,
                t.Name,
                t.Slug,
                t.PageInfos!.Count(p => !p.IsDeleted)))
            .ToListAsync(cancellationToken);

        return Result.Success(tags);
    }
}
