using Domain.Entities.Workflows;

namespace Application.Features.Queries.Workflows.GetWorkflowDefinitions;

public sealed record WorkflowStepResponse(int StepOrder, Guid RoleId, string RoleName);
