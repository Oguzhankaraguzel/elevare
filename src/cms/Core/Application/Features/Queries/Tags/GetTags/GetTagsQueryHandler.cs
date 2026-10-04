using Application.Abstraction.Data;
using Domain.Entities.Tags;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Tags.GetTags;

internal sealed class GetTagsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetTagsQuery, List<TagResponse>>
{
    public async Task<Result<List<TagResponse>>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        List<TagResponse> tags = await db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse(t.Id, t.Name, t.Slug))
            .ToListAsync(cancellationToken);

        return Result.Success(tags);
    }
}
