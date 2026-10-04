using Application.Abstraction.Data;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.ContentBulkEdits.RevertContentBulkEdit;

internal sealed class RevertContentBulkEditCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<RevertContentBulkEditCommand>
{
    public async Task<Result> Handle(RevertContentBulkEditCommand request, CancellationToken cancellationToken)
    {
        ContentBulkEdit? bulkEdit = await db.ContentBulkEdits
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (bulkEdit is null)
            return Result.Failure(ContentBulkEditErrors.NotFound);

        if (bulkEdit.IsReverted)
            return Result.Failure(ContentBulkEditErrors.AlreadyReverted);

        List<int> pageIds = [.. bulkEdit.Items.Select(i => i.PageInfoId)];

        if (bulkEdit.Kind == ContentBulkEditKind.StructuredDataRefresh)
        {
            List<PageInfo> pages = await db.PageInfos
                .Where(p => pageIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (ContentBulkEditItem item in bulkEdit.Items)
            {
                PageInfo? page = pages.FirstOrDefault(p => p.Id == item.PageInfoId);
                if (page is null)
                    continue; // page since removed — nothing left to restore

                page.SeoMeta.StructuredData = item.OldStructuredData;
            }
        }
        else
        {
            List<PageContent> contents = await db.PageContents
                .Where(c => pageIds.Contains(c.PageInfoId))
                .ToListAsync(cancellationToken);

            foreach (ContentBulkEditItem item in bulkEdit.Items)
            {
                PageContent? content = contents.FirstOrDefault(c => c.PageInfoId == item.PageInfoId);
                if (content is null)
                    continue; // page/content since removed — nothing left to restore

                content.GjsHtml = item.OldGjsHtml;
                content.GjsCss = item.OldGjsCss;
                content.GjsData = item.OldGjsData;
            }
        }

        bulkEdit.IsReverted = true;
        bulkEdit.RevertedDate = DateTime.UtcNow;

        return Result.Success();
    }
}
