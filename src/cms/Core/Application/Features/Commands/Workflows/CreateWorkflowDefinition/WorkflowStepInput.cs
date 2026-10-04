namespace Application.Features.Commands.Workflows.CreateWorkflowDefinition;

/// <summary>
/// One stage of a workflow being created. <paramref name="RequiredUserId"/> is
/// optional: leave it null to let the whole role decide the step, or name a person
/// to make the step theirs alone.
/// </summary>
public sealed record WorkflowStepInput(Guid RequiredRoleId, Guid? RequiredUserId = null);
