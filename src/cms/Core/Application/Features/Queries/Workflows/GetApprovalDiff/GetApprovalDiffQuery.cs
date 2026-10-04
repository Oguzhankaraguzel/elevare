using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Workflows.GetApprovalDiff;

/// <summary>
/// What actually changed in the content an approver is about to sign off on.
/// </summary>
public sealed record GetApprovalDiffQuery(int RequestId) : IQuery<ApprovalDiffResponse>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.ApprovalsDecide;
}
