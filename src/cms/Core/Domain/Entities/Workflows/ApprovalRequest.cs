using Domain.Entities.Abstractions;

namespace Domain.Entities.Workflows;

/// <summary>
/// Tracks a single content item's progress through its <see cref="WorkflowDefinition"/>.
/// The requester is <see cref="Domain.Entities.Abstractions.BaseEntity.CreateUserId"/>
/// (whoever's save first staged this content while the workflow was active).
/// </summary>
public class ApprovalRequest : BaseEntity
{
    public WorkflowContentType ContentType { get; set; }

    /// <summary>Id of the <c>PageInfo</c> or <c>PageTemplate</c> row this request covers.</summary>
    public int ContentId { get; set; }

    public int WorkflowDefinitionId { get; set; }

    public int CurrentStepOrder { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public virtual ICollection<ApprovalStepDecision> Decisions { get; set; } = [];
}
