using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;

namespace Domain.Entities.Workflows;

/// <summary>
/// One recorded decision against an <see cref="ApprovalRequest"/>. The decider is
/// <see cref="Domain.Entities.Abstractions.BaseEntity.CreateUserId"/> (whoever
/// submitted this decision).
/// </summary>
public class ApprovalStepDecision : BaseEntity
{
    public int ApprovalRequestId { get; set; }

    public int StepOrder { get; set; }

    public ApprovalDecisionType Decision { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }

    public virtual ApprovalRequest ApprovalRequest { get; set; } = null!;
}
