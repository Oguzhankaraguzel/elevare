using SharedKernel.Concrete;

namespace Domain.Entities.Workflows;

public static class WorkflowErrors
{
    public static readonly Error DefinitionNotFound = Error.NotFound("Workflow.DefinitionNotFound", "The workflow definition was not found.");
    public static readonly Error NeedsAtLeastOneStep = Error.Failure("Workflow.NeedsAtLeastOneStep", "A workflow needs at least one step.");
    public static readonly Error CannotDeleteActiveDefinition = Error.Failure("Workflow.CannotDeleteActiveDefinition", "Deactivate this workflow before deleting it.");

    /// <summary>
    /// Raised when a workflow still has content waiting on it. Deleting it would
    /// strand that content: the edit stays in the preview columns, never reaches the
    /// live site, and disappears from the approvals queue with no way to release it.
    /// The FK is Restrict, but deletes here are soft, so the database never catches
    /// this — the check has to live in the handler.
    /// </summary>
    public static readonly Error CannotDeleteWithPendingRequests = Error.Conflict(
        "Workflow.CannotDeleteWithPendingRequests",
        "This workflow still has content waiting for approval. Decide or reject those requests first — deleting now would leave those edits stuck and invisible.");

    public static readonly Error RequestNotFound = Error.NotFound("Workflow.RequestNotFound", "The approval request was not found.");
    public static readonly Error RequestNotPending = Error.Conflict("Workflow.RequestNotPending", "This request has already been decided.");
    public static readonly Error NotAuthorizedForStep = Error.Failure("Workflow.NotAuthorizedForStep", "You are not a member of the role required to decide this step.");
}
