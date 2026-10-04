using Application.Abstraction.Data;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Workflows;

/// <summary>
/// Bridges a content save into the approval-workflow tables. Mirrors the shape of
/// <c>RedirectResolution</c>/<c>PageHierarchy</c> — a plain static helper called
/// from the content's own command handler, not a separate CQRS feature, since it
/// only ever runs as a side effect of that save.
/// </summary>
public static class ApprovalWorkflowResolution
{
    /// <summary>
    /// If an active <see cref="WorkflowDefinition"/> gates <paramref name="contentType"/>,
    /// creates (or reuses the still-pending) <see cref="ApprovalRequest"/> for this
    /// content and returns <c>true</c> — the caller must then write the new HTML/CSS
    /// into the content's Preview* columns instead of its live ones. Returns
    /// <c>false</c> when nothing gates this content type, meaning the caller should
    /// publish directly, exactly as before workflows existed.
    /// </summary>
    /// <param name="alsoGovernedBy">
    /// Checked only when nothing gates <paramref name="contentType"/> directly. A
    /// linked page template is live page content the moment it's dropped onto a page
    /// — it isn't drafted and reviewed the way the page itself is, it just changes on
    /// save. If a site gates Sayfa but nobody has separately gated Şablon, an editor
    /// could move content into a linked template and publish straight past the review
    /// the page requires. <see cref="Commands.PageTemplates.UpdatePageTemplate.UpdatePageTemplateCommandHandler"/>
    /// passes <see cref="WorkflowContentType.Page"/> here for linked templates so that
    /// gap is closed without asking anyone to define the same approval chain twice;
    /// every other caller leaves it null.
    /// </param>
    public static async Task<bool> StageIfWorkflowActiveAsync(
        ICmsApplicationDbContext db, WorkflowContentType contentType, int contentId, CancellationToken cancellationToken,
        WorkflowContentType? alsoGovernedBy = null)
    {
        WorkflowDefinition? workflow = await db.WorkflowDefinitions
            .Include(w => w.Steps)
            .Where(w => w.ContentType == contentType && w.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (workflow is null && alsoGovernedBy is WorkflowContentType fallbackType)
        {
            workflow = await db.WorkflowDefinitions
                .Include(w => w.Steps)
                .Where(w => w.ContentType == fallbackType && w.IsActive)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (workflow is null || workflow.Steps.Count == 0)
            return false;

        bool alreadyPending = await db.ApprovalRequests
            .AnyAsync(a => a.ContentType == contentType && a.ContentId == contentId && a.Status == ApprovalStatus.Pending, cancellationToken);

        // An edit made while a request is already pending just re-stages the content
        // under that same request — it isn't restarted at step 1. A prior Approved/
        // Rejected request is terminal, so this save starts a fresh one.
        if (!alreadyPending)
        {
            int firstStep = workflow.Steps.Min(s => s.StepOrder);
            db.ApprovalRequests.Add(new ApprovalRequest
            {
                ContentType = contentType,
                ContentId = contentId,
                WorkflowDefinitionId = workflow.Id,
                CurrentStepOrder = firstStep,
                Status = ApprovalStatus.Pending,
            });
        }

        return true;
    }

    /// <summary>
    /// True when this content has a request still awaiting a decision — i.e. the
    /// live content has not been through the round its author most recently
    /// submitted. Callers that flip a status flag outside <c>StageIfWorkflowActiveAsync</c>
    /// (the Pages list's quick-publish action, which never touches GjsHtml) use this
    /// to avoid reporting a page Published while that round is still open.
    /// </summary>
    public static Task<bool> HasPendingApprovalAsync(
        ICmsApplicationDbContext db, WorkflowContentType contentType, int contentId, CancellationToken cancellationToken)
        => db.ApprovalRequests.AnyAsync(a =>
            a.ContentType == contentType && a.ContentId == contentId && a.Status == ApprovalStatus.Pending,
            cancellationToken);
}
