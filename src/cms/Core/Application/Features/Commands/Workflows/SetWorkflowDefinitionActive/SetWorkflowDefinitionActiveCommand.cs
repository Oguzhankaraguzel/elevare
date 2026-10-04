using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Workflows.SetWorkflowDefinitionActive;

public sealed record SetWorkflowDefinitionActiveCommand(int Id, bool IsActive) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.WorkflowsManage;
}
