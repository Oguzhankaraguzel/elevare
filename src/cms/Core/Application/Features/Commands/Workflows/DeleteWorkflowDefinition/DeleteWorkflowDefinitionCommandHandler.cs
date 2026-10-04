using Application.Abstraction.Data;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Workflows.DeleteWorkflowDefinition;

internal sealed class DeleteWorkflowDefinitionCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<DeleteWorkflowDefinitionCommand>
{
    public async Task<Result> Handle(DeleteWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        WorkflowDefinition? workflow = await db.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (workflow is null)
            return Result.Failure(WorkflowErrors.DefinitionNotFound);

        if (workflow.IsActive)
            return Result.Failure(WorkflowErrors.CannotDeleteActiveDefinition);

        // Deactivating a workflow does not finish what is already moving through it.
        // Those requests keep their staged edit in the preview columns and vanish
        // from the queue the moment this row goes, so the edit can never be released
        // or rejected — it is simply lost with no trace an operator can follow.
        bool hasPending = await db.ApprovalRequests
            .AnyAsync(a => a.WorkflowDefinitionId == workflow.Id && a.Status == ApprovalStatus.Pending,
                cancellationToken);

        if (hasPending)
            return Result.Failure(WorkflowErrors.CannotDeleteWithPendingRequests);

        db.WorkflowDefinitions.Remove(workflow);

        return Result.Success();
    }
}
