using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Workflows;

internal sealed class WorkflowStepConfiguration : BaseEntityConfiguration<WorkflowStep>
{
    public override void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        base.Configure(builder);

        // Roles are managed by Identity, not soft-deleted like BaseEntity rows —
        // Restrict so a role can't be removed out from under a workflow step.
        builder.HasOne(s => s.RequiredRole)
            .WithMany()
            .HasForeignKey(s => s.RequiredRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Same reasoning as the role: a step that names a person must not be left
        // pointing at a user row that has been removed underneath it.
        builder.HasOne(s => s.RequiredUser)
            .WithMany()
            .HasForeignKey(s => s.RequiredUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered to live rows — deletes are soft (see ApplicationDbContext.ApplySoftDeletes),
        // so replacing a definition's steps must not collide with the old, now-deleted ones.
        builder.HasIndex(s => new { s.WorkflowDefinitionId, s.StepOrder }).IsUnique()
            .HasDatabaseName("UX_WorkflowSteps_Definition_Order").HasFilter("\"IsDeleted\" = false");
    }
}
