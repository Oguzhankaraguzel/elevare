using Application.Features.Commands.Pages.Shared;
using Application.Abstraction.Data;
using Application.Features.Commands.Pages;
using Application.Features.Workflows;
using Domain.Entities.PageInfos;
using Domain.Entities.Redirects;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Pages.UpdatePageStatus;

internal sealed class UpdatePageStatusCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<UpdatePageStatusCommand>
{
    public async Task<Result> Handle(UpdatePageStatusCommand request, CancellationToken cancellationToken)
    {
        // Content too: an Article block's date is the page's publish date (PagePublication).
        PageInfo? page = await db.PageInfos
            .Include(p => p.Content)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (page is null)
            return Result.Failure(PageInfoErrors.NotFound);

        bool leavingPublished = page.PageStatus == PageStatus.Published && request.Status != PageStatus.Published;
        if (await HomePageUnpublish.CheckAsync(db, page, leavingPublished, request.ConfirmHomePageUnpublish, cancellationToken) is Error homeError)
            return Result.Failure(homeError);

        // This quick action never touches GjsHtml, but it must still not let a page
        // report Published while its last save is stuck in Preview* awaiting review
        // — see PageInfo.PendingStatus for why the two are kept in lockstep.
        bool goingLiveUnreviewed = request.Status == PageStatus.Published
            && page.PageStatus != PageStatus.Published
            && await ApprovalWorkflowResolution.HasPendingApprovalAsync(
                db, WorkflowContentType.Page, page.Id, cancellationToken);

        if (goingLiveUnreviewed)
            page.PendingStatus = request.Status;
        else
        {
            page.PageStatus = request.Status;
            page.PendingStatus = null;
        }

        PagePublication.Stamp(page, DateTime.UtcNow);

        // Best-effort: a redirect couldn't be created shouldn't block the status change itself.
        if (leavingPublished)
            await RedirectResolution.UpsertForPageRemovedAsync(
                db, page.Id, page.FullSlug, request.RedirectTargetUrl, RedirectReason.PageArchived, cancellationToken);

        return Result.Success();
    }
}
