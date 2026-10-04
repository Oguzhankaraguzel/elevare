using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.FormReplyTemplates;

internal sealed class FormReplyTemplateConfiguration : BaseEntityConfiguration<FormReplyTemplate>
{
    public override void Configure(EntityTypeBuilder<FormReplyTemplate> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(300);
        builder.Property(t => t.Body).IsRequired();

        builder.HasIndex(t => t.SortOrder);
    }
}
