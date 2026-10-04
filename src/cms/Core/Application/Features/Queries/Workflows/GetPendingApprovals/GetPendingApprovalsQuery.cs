using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Workflows.GetPendingApprovals;

public sealed record GetPendingApprovalsQuery : IQuery<List<ApprovalRequestResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.ApprovalsDecide;
}

