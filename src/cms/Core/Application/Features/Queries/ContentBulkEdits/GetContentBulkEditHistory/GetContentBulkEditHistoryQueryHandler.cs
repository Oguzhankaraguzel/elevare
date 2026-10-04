using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.ContentBulkEdits.GetContentBulkEditHistory;

internal sealed class GetContentBulkEditHistoryQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetContentBulkEditHistoryQuery, List<ContentBulkEditHistoryItemResponse>>
{
    public async Task<Result<List<ContentBulkEditHistoryItemResponse>>> Handle(
        GetContentBulkEditHistoryQuery request,
        CancellationToken cancellationToken)
    {
        List<ContentBulkEditHistoryItemResponse> items = await db.ContentBulkEdits
            .AsNoTracking()
            .OrderByDescending(e => e.CreateDate)
            .Select(e => new ContentBulkEditHistoryItemResponse(
                e.Id,
                e.Kind,
                e.SearchText,
                e.ReplaceText,
                e.AffectedPageCount,
                e.IsReverted,
                e.CreateDate,
                e.RevertedDate,
                e.Items
                    .Select(i => new ContentBulkEditAffectedPageResponse(
                        i.PageInfoId, i.PageTitleSnapshot, i.PageFullSlugSnapshot, i.MatchCount))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
