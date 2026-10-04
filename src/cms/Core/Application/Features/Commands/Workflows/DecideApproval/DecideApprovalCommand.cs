using Domain.Entities.Workflows;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Workflows.DecideApproval;

public sealed record DecideApprovalCommand(int ApprovalRequestId, ApprovalDecisionType Decision, string? Comment) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.ApprovalsDecide;
}
