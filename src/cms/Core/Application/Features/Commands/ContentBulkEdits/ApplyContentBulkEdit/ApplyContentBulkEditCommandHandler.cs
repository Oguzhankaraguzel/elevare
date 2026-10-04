using Application.Abstraction.Data;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.PageContents;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.ContentBulkEdits.ApplyContentBulkEdit;

internal sealed class ApplyContentBulkEditCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<ApplyContentBulkEditCommand, ApplyContentBulkEditResponse>
{
    public async Task<Result<ApplyContentBulkEditResponse>> Handle(
        ApplyContentBulkEditCommand request,
        CancellationToken cancellationToken)
    {
        List<PageContent> contents = await db.PageContents
            .Include(c => c.PageInfo)
            .Where(c => request.PageInfoIds.Contains(c.PageInfoId))
            .ToListAsync(cancellationToken);

        var bulkEdit = new ContentBulkEdit
        {
            Kind = ContentBulkEditKind.TextReplace,
            SearchText = request.SearchText,
            ReplaceText = request.ReplaceText,
        };
        db.ContentBulkEdits.Add(bulkEdit);

        int affected = 0;
        foreach (PageContent content in contents)
        {
            int matchCount = CountOccurrences(content.GjsHtml, request.SearchText)
                + CountOccurrences(content.GjsCss, request.SearchText)
                + CountOccurrences(content.GjsData, request.SearchText);

            // Content may have changed since the search step (e.g. edited in the
            // builder in the meantime) — skip rather than snapshot a stale/wrong diff.
            if (matchCount == 0)
                continue;

            bulkEdit.Items.Add(new ContentBulkEditItem
            {
                ContentBulkEdit = bulkEdit,
                PageInfoId = content.PageInfoId,
                PageTitleSnapshot = content.PageInfo.SeoMeta.Title,
                PageFullSlugSnapshot = content.PageInfo.FullSlug,
                MatchCount = matchCount,
                OldGjsHtml = content.GjsHtml,
                OldGjsCss = content.GjsCss,
                OldGjsData = content.GjsData,
            });

            content.GjsHtml = content.GjsHtml?.Replace(request.SearchText, request.ReplaceText);
            content.GjsCss = content.GjsCss?.Replace(request.SearchText, request.ReplaceText);
            content.GjsData = content.GjsData?.Replace(request.SearchText, request.ReplaceText);

            affected++;
        }

        bulkEdit.AffectedPageCount = affected;

        // Save explicitly so the generated identity is available for the response;
        // the SaveChanges pipeline behavior then runs as a harmless no-op.
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new ApplyContentBulkEditResponse(bulkEdit.Id, affected));
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
