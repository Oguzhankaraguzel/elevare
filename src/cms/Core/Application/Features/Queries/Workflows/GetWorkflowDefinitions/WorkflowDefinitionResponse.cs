using Domain.Entities.Workflows;

namespace Application.Features.Queries.Workflows.GetWorkflowDefinitions;

public sealed record WorkflowDefinitionResponse(
    int Id, string Name, WorkflowContentType ContentType, bool IsActive, List<WorkflowStepResponse> Steps);
