using Application.Abstraction.Data;
using Domain.Entities.Workflows;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Workflows.CreateWorkflowDefinition;

internal sealed class CreateWorkflowDefinitionCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<CreateWorkflowDefinitionCommand, int>
{
    public async Task<Result<int>> Handle(CreateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        if (request.Steps.Count == 0)
            return Result.Failure<int>(WorkflowErrors.NeedsAtLeastOneStep);

        var workflow = new WorkflowDefinition
        {
            Name = request.Name.Trim(),
            ContentType = request.ContentType,
            IsActive = false,
        };

        for (int i = 0; i < request.Steps.Count; i++)
        {
            workflow.Steps.Add(new WorkflowStep
            {
                StepOrder = i + 1,
                RequiredRoleId = request.Steps[i].RequiredRoleId,
                RequiredUserId = request.Steps[i].RequiredUserId,
            });
        }

        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(workflow.Id);
    }
}
