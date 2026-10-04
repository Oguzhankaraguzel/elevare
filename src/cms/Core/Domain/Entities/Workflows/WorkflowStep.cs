using Domain.Entities.Abstractions;
using Domain.Entities.Users;

namespace Domain.Entities.Workflows;

/// <summary>
/// One ordered stage in a <see cref="WorkflowDefinition"/>.
/// <para>
/// A step is answerable either by a whole role or by one named person. The role is
/// always required — it is what the queue and the UI describe the step as — and
/// <see cref="RequiredUserId"/> narrows it when the step belongs to a specific
/// individual ("the finance director signs this off", not "someone in finance").
/// </para>
/// </summary>
public class WorkflowStep : BaseEntity
{
    public int WorkflowDefinitionId { get; set; }

    /// <summary>1-based position in the chain; the lowest pending order is decided first.</summary>
    public int StepOrder { get; set; }

    public Guid RequiredRoleId { get; set; }

    /// <summary>
    /// When set, only this user may decide the step — membership of
    /// <see cref="RequiredRole"/> is no longer sufficient on its own.
    /// Null leaves the step open to the whole role.
    /// </summary>
    public Guid? RequiredUserId { get; set; }

    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public virtual AppRole RequiredRole { get; set; } = null!;
    public virtual AppUser? RequiredUser { get; set; }
}
