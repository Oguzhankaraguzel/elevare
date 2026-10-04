using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Workflows;

internal sealed class ApprovalRequestConfiguration : BaseEntityConfiguration<ApprovalRequest>
{
    public override void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        base.Configure(builder);

        builder.HasOne(a => a.WorkflowDefinition)
            .WithMany()
            .HasForeignKey(a => a.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Decisions)
            .WithOne(d => d.ApprovalRequest)
            .HasForeignKey(d => d.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Looked up on every save of a workflow-gated content item, and by the
        // approvals queue (filtered to Status == Pending).
        builder.HasIndex(a => new { a.ContentType, a.ContentId, a.Status }).HasDatabaseName("IX_ApprovalRequests_Content_Status");
    }
}
