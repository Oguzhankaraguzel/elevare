using Application.Abstraction.Data;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Workflows.SetWorkflowDefinitionActive;

internal sealed class SetWorkflowDefinitionActiveCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<SetWorkflowDefinitionActiveCommand>
{
    public async Task<Result> Handle(SetWorkflowDefinitionActiveCommand request, CancellationToken cancellationToken)
    {
        WorkflowDefinition? workflow = await db.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (workflow is null)
            return Result.Failure(WorkflowErrors.DefinitionNotFound);

        if (request.IsActive)
        {
            // Only one workflow may gate a given content type at a time.
            List<WorkflowDefinition> siblings = await db.WorkflowDefinitions
                .Where(w => w.ContentType == workflow.ContentType && w.Id != workflow.Id && w.IsActive)
                .ToListAsync(cancellationToken);
            foreach (WorkflowDefinition sibling in siblings)
                sibling.IsActive = false;
        }

        workflow.IsActive = request.IsActive;

        return Result.Success();
    }
}
