using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Workflows.DeleteWorkflowDefinition;

public sealed record DeleteWorkflowDefinitionCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.WorkflowsManage;
}
