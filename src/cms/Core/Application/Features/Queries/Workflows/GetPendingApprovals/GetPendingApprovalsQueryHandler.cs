using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.PageInfos;
using Domain.Entities.PageTemplates;
using Domain.Entities.Users;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Queries.Workflows.GetPendingApprovals;

internal sealed class GetPendingApprovalsQueryHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : IQueryHandler<GetPendingApprovalsQuery, List<ApprovalRequestResponse>>
{
    public async Task<Result<List<ApprovalRequestResponse>>> Handle(GetPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        List<ApprovalRequest> approvals = await db.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending)
            .Include(a => a.CreateUser)
            .Include(a => a.WorkflowDefinition)
                .ThenInclude(w => w.Steps)
                    .ThenInclude(s => s.RequiredRole)
            .Include(a => a.WorkflowDefinition)
                .ThenInclude(w => w.Steps)
                    .ThenInclude(s => s.RequiredUser)
            .OrderBy(a => a.CreateDate)
            .ToListAsync(cancellationToken);

        var pageIds = approvals.Where(a => a.ContentType == WorkflowContentType.Page).Select(a => a.ContentId).ToList();
        var templateIds = approvals.Where(a => a.ContentType == WorkflowContentType.PageTemplate).Select(a => a.ContentId).ToList();

        Dictionary<int, string> pageTitles = await db.PageInfos
            .Where(p => pageIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.SeoMeta.Title.HasValue() ? p.SeoMeta.Title : p.Slug, cancellationToken);

        Dictionary<int, string> templateNames = await db.PageTemplates
            .Where(t => templateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        List<ApprovalRequestResponse> responses = [];
        foreach (ApprovalRequest approval in approvals)
        {
            WorkflowStep? currentStep = approval.WorkflowDefinition.Steps
                .FirstOrDefault(s => s.StepOrder == approval.CurrentStepOrder);

            string contentTitle = approval.ContentType == WorkflowContentType.Page
                ? pageTitles.GetValueOrDefault(approval.ContentId, "—")
                : templateNames.GetValueOrDefault(approval.ContentId, "—");

            // Mirrors DecideApprovalCommandHandler.IsAllowedToDecide — a step that
            // names a person is theirs alone, so the queue must not offer the button
            // to the rest of the role and have the command refuse it.
            bool canDecide = currentStep is not null
                && (userContext.IsAdminOrAbove
                    || (currentStep.RequiredUserId is { } required
                        ? userContext.UserId == required
                        : userContext.IsInRole(currentStep.RequiredRole.Name ?? "")));

            responses.Add(new ApprovalRequestResponse(
                approval.Id,
                approval.ContentType,
                approval.ContentId,
                contentTitle,
                approval.WorkflowDefinition.Name,
                approval.CurrentStepOrder,
                approval.WorkflowDefinition.Steps.Count,
                currentStep?.RequiredRole.Name ?? "",
                currentStep?.RequiredUser is { } stepUser ? RequesterName(stepUser) : null,
                RequesterName(approval.CreateUser),
                approval.CreateDate,
                canDecide));
        }

        return Result.Success(responses);
    }

    private static string RequesterName(AppUser? user)
    {
        if (user is null) return "—";
        return user.FullName.HasValue() ? user.FullName : user.UserName ?? "—";
    }
}
