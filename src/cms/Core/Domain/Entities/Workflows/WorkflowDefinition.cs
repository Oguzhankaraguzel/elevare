using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;

namespace Domain.Entities.Workflows;

/// <summary>
/// A named approval chain for a content type (e.g. "Sayfa Onayı") — while
/// <see cref="Domain.Entities.Abstractions.BaseEntity.IsActive"/> is true, every
/// save of that content type is staged into the content's existing preview
/// columns instead of publishing live, until it has passed every
/// <see cref="Steps"/> in order. At most one definition per
/// <see cref="ContentType"/> is active at a time (enforced by the command that
/// flips this flag, not by the database).
/// </summary>
public class WorkflowDefinition : BaseEntity
{
    [MaxLength(200)]
    public required string Name { get; set; }

    public WorkflowContentType ContentType { get; set; }

    public virtual ICollection<WorkflowStep> Steps { get; set; } = [];
}
