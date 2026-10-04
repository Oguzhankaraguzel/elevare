using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Workflows;

internal sealed class WorkflowDefinitionConfiguration : BaseEntityConfiguration<WorkflowDefinition>
{
    public override void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        base.Configure(builder);

        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();

        builder.HasMany(w => w.Steps)
            .WithOne(s => s.WorkflowDefinition)
            .HasForeignKey(s => s.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
