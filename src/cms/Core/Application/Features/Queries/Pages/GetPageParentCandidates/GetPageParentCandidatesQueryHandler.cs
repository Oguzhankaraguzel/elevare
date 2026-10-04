using Application.Abstraction.Data;
using Application.Features.Commands.Pages;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPageParentCandidates;

internal sealed class GetPageParentCandidatesQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageParentCandidatesQuery, List<PageParentCandidateResponse>>
{
    public async Task<Result<List<PageParentCandidateResponse>>> Handle(
        GetPageParentCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        List<PageInfo> pages = await db.PageInfos
            .AsNoTracking()
            .Where(p => p.LanguageId == request.LanguageId)
            .ToListAsync(cancellationToken);

        var byId = pages.ToDictionary(p => p.Id);

        int GetLevel(int id)
        {
            int level = 1;
            int? currentParentId = byId.TryGetValue(id, out PageInfo? self) ? self.ParentPageId : null;
            while (currentParentId is int parentId && byId.TryGetValue(parentId, out PageInfo? parent))
            {
                level++;
                currentParentId = parent.ParentPageId;
            }
            return level;
        }

        HashSet<int> excluded = [];
        if (request.ExcludePageId is int excludeId)
        {
            excluded.Add(excludeId);
            Queue<int> queue = new();
            queue.Enqueue(excludeId);
            while (queue.Count > 0)
            {
                int id = queue.Dequeue();
                foreach (PageInfo child in pages.Where(p => p.ParentPageId == id))
                {
                    if (excluded.Add(child.Id))
                        queue.Enqueue(child.Id);
                }
            }
        }

        List<PageParentCandidateResponse> result = [.. pages
            .Where(p => !excluded.Contains(p.Id))
            .Select(p => new PageParentCandidateResponse(p.Id, p.SeoMeta.Title, p.FullSlug, GetLevel(p.Id)))
            .Where(c => c.Level < PageHierarchy.MaxDepth)
            .OrderBy(c => c.FullSlug)];

        return Result.Success(result);
    }
}
