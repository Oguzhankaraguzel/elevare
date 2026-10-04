using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Workflows.GetWorkflowDefinitions;

public sealed record GetWorkflowDefinitionsQuery : IQuery<List<WorkflowDefinitionResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.WorkflowsManage;
}

