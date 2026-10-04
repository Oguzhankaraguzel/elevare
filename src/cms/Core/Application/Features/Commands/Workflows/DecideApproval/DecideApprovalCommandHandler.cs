using Application.Features.Commands.Pages.Shared;
using Application.Features.Commands.PageTemplates.Shared;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Workflows.DecideApproval;

internal sealed class DecideApprovalCommandHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : ICommandHandler<DecideApprovalCommand>
{
    public async Task<Result> Handle(DecideApprovalCommand request, CancellationToken cancellationToken)
    {
        ApprovalRequest? approval = await db.ApprovalRequests
            .Include(a => a.WorkflowDefinition)
                .ThenInclude(w => w.Steps)
                    .ThenInclude(s => s.RequiredRole)
            .FirstOrDefaultAsync(a => a.Id == request.ApprovalRequestId, cancellationToken);

        if (approval is null)
            return Result.Failure(WorkflowErrors.RequestNotFound);

        if (approval.Status != ApprovalStatus.Pending)
            return Result.Failure(WorkflowErrors.RequestNotPending);

        WorkflowStep? currentStep = approval.WorkflowDefinition.Steps
            .FirstOrDefault(s => s.StepOrder == approval.CurrentStepOrder);

        if (currentStep is null)
            return Result.Failure(WorkflowErrors.DefinitionNotFound);

        if (!IsAllowedToDecide(currentStep))
            return Result.Failure(WorkflowErrors.NotAuthorizedForStep);

        db.ApprovalStepDecisions.Add(new ApprovalStepDecision
        {
            ApprovalRequestId = approval.Id,
            StepOrder = currentStep.StepOrder,
            Decision = request.Decision,
            Comment = request.Comment,
        });

        if (request.Decision == ApprovalDecisionType.Rejected)
        {
            // Terminal — the staged Preview* content is left in place so the
            // requester can still see exactly what was rejected via the existing
            // preview link; their next save starts a brand new request.
            approval.Status = ApprovalStatus.Rejected;
            return Result.Success();
        }

        WorkflowStep? nextStep = approval.WorkflowDefinition.Steps
            .Where(s => s.StepOrder > currentStep.StepOrder)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault();

        if (nextStep is not null)
        {
            approval.CurrentStepOrder = nextStep.StepOrder;
            return Result.Success();
        }

        // Last step approved — promote the staged content to live.
        approval.Status = ApprovalStatus.Approved;

        if (approval.ContentType == WorkflowContentType.Page)
        {
            PageInfo? page = await db.PageInfos
                .Include(p => p.Content)
                .FirstOrDefaultAsync(p => p.Id == approval.ContentId, cancellationToken);

            // A later save can publish directly and leave this request orphaned:
            // deactivate the workflow (or an editor's next save simply lands after
            // nothing gates Page anymore) and that save already promoted its own
            // content straight to live, clearing Preview* to null in the process.
            // This request is still sitting there Pending because nothing ever
            // resolves a request its own content bypassed. Approving it blind used
            // to copy that null over GjsHtml — silently blanking a page a separate,
            // more recent save had already published.
            if (page?.Content is { PreviewGjsHtml: not null } or { PreviewGjsCss: not null })
            {
                page.Content.GjsHtml = page.Content.PreviewGjsHtml;
                page.Content.GjsCss = page.Content.PreviewGjsCss;
                page.Content.PreviewGjsHtml = null;
                page.Content.PreviewGjsCss = null;
            }

            // The status the author asked for was held back the same way as the
            // HTML above — flip it now that the chain has actually cleared.
            if (page is { PendingStatus: { } pendingStatus })
            {
                page.PageStatus = pendingStatus;
                page.PendingStatus = null;
            }

            if (page is not null)
                PagePublication.Stamp(page, DateTime.UtcNow);
        }
        else
        {
            PageTemplate? template = await db.PageTemplates
                .FirstOrDefaultAsync(t => t.Id == approval.ContentId, cancellationToken);

            // Same reasoning as the page branch above.
            if (template is { PreviewGjsHtml: not null } or { PreviewGjsCss: not null })
            {
                TemplateOwnContent.Apply(template, template.PreviewGjsHtml);
                template.GjsHtml = template.PreviewGjsHtml;
                template.GjsCss = template.PreviewGjsCss;
                template.PreviewGjsHtml = null;
                template.PreviewGjsCss = null;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Who may answer this step.
    /// <para>
    /// A step that names a user is theirs alone — role membership no longer stands
    /// in for it, because "the finance director signs this off" is a different
    /// statement from "someone in finance does". Admins keep their override either
    /// way: a workflow must not be able to deadlock the site when the named person
    /// leaves, and that escape hatch is the reason the override exists.
    /// </para>
    /// </summary>
    private bool IsAllowedToDecide(WorkflowStep step)
    {
        if (userContext.IsAdminOrAbove)
            return true;

        if (step.RequiredUserId is { } requiredUser)
            return userContext.UserId == requiredUser;

        return userContext.IsInRole(step.RequiredRole.Name!);
    }
}
