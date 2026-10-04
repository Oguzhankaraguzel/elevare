using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Workflows;

internal sealed class ApprovalStepDecisionConfiguration : BaseEntityConfiguration<ApprovalStepDecision>
{
    public override void Configure(EntityTypeBuilder<ApprovalStepDecision> builder)
    {
        base.Configure(builder);

        builder.Property(d => d.Comment).HasMaxLength(2000);
    }
}
