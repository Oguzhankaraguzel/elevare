using Application.Abstraction.Data;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Workflows.GetWorkflowDefinitions;

internal sealed class GetWorkflowDefinitionsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetWorkflowDefinitionsQuery, List<WorkflowDefinitionResponse>>
{
    public async Task<Result<List<WorkflowDefinitionResponse>>> Handle(GetWorkflowDefinitionsQuery request, CancellationToken cancellationToken)
    {
        List<WorkflowDefinition> workflows = await db.WorkflowDefinitions
            .Include(w => w.Steps)
                .ThenInclude(s => s.RequiredRole)
            .OrderBy(w => w.ContentType).ThenBy(w => w.Name)
            .ToListAsync(cancellationToken);

        var responses = workflows.Select(w => new WorkflowDefinitionResponse(
            w.Id, w.Name, w.ContentType, w.IsActive,
            w.Steps
                .OrderBy(s => s.StepOrder)
                .Select(s => new WorkflowStepResponse(s.StepOrder, s.RequiredRoleId, s.RequiredRole.Name ?? ""))
                .ToList()))
            .ToList();

        return Result.Success(responses);
    }
}
