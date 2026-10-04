using Domain.Entities.Workflows;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Workflows.CreateWorkflowDefinition;

/// <summary>Creates a new, initially inactive workflow with its ordered steps. Activate it separately via <c>SetWorkflowDefinitionActiveCommand</c>.</summary>
public sealed record CreateWorkflowDefinitionCommand(
    string Name, WorkflowContentType ContentType, List<WorkflowStepInput> Steps) : ICommand<int>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.WorkflowsManage;
}
