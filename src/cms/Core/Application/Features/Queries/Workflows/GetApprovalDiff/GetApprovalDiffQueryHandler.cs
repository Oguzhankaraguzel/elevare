using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Queries.Workflows.GetApprovalDiff;

internal sealed class GetApprovalDiffQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetApprovalDiffQuery, ApprovalDiffResponse>
{
    public async Task<Result<ApprovalDiffResponse>> Handle(GetApprovalDiffQuery request, CancellationToken cancellationToken)
    {
        ApprovalRequest? approval = await db.ApprovalRequests
            .FirstOrDefaultAsync(a => a.Id == request.RequestId, cancellationToken);

        if (approval is null)
            return Result.Failure<ApprovalDiffResponse>(WorkflowErrors.RequestNotFound);

        return approval.ContentType == WorkflowContentType.Page
            ? await PageDiffAsync(approval.ContentId, cancellationToken)
            : await TemplateDiffAsync(approval.ContentId, cancellationToken);
    }

    private async Task<Result<ApprovalDiffResponse>> PageDiffAsync(int pageId, CancellationToken cancellationToken)
    {
        var page = await db.PageInfos
            .Where(p => p.Id == pageId)
            .Select(p => new
            {
                Title = p.SeoMeta.Title.HasValue() ? p.SeoMeta.Title : p.Slug,
                p.PageStatus,
                p.PendingStatus,
                LiveHtml = p.Content!.GjsHtml,
                StagedHtml = p.Content!.PreviewGjsHtml,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (page is null)
            return Result.Failure<ApprovalDiffResponse>(PageInfoErrors.NotFound);

        return Result.Success(Build(page.Title, page.LiveHtml, page.StagedHtml, page.PageStatus, page.PendingStatus));
    }

    private async Task<Result<ApprovalDiffResponse>> TemplateDiffAsync(int templateId, CancellationToken cancellationToken)
    {
        var template = await db.PageTemplates
            .Where(t => t.Id == templateId)
            .Select(t => new { t.Name, t.GjsHtml, t.PreviewGjsHtml })
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            return Result.Failure<ApprovalDiffResponse>(PageTemplateErrors.NotFound);

        // Templates carry no publish status of their own — they are live the moment
        // the pages referencing them are.
        return Result.Success(Build(template.Name, template.GjsHtml, template.PreviewGjsHtml, null, null));
    }

    private static ApprovalDiffResponse Build(
        string title,
        string? liveHtml,
        string? stagedHtml,
        PageStatus? currentStatus,
        PageStatus? requestedStatus)
    {
        // Nothing staged means the request is gating a status change only (or the
        // content predates the workflow). Saying "no changes" would be wrong and
        // alarming, so the UI gets a flag instead of an empty diff.
        if (stagedHtml is null)
            return new ApprovalDiffResponse(title, false, false, currentStatus, requestedStatus, 0, 0, []);

        List<ApprovalDiffLine> lines = HtmlTextDiff.Compare(liveHtml, stagedHtml);

        return new ApprovalDiffResponse(
            title,
            HasStagedContent: true,
            IsFirstPublish: liveHtml.IsNullOrWhiteSpace(),
            currentStatus,
            requestedStatus,
            AddedCount: lines.Count(l => l.Kind == ApprovalDiffKind.Added),
            RemovedCount: lines.Count(l => l.Kind == ApprovalDiffKind.Removed),
            lines);
    }
}
