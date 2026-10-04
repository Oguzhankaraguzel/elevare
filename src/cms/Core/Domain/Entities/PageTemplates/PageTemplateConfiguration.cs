using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.PageTemplates;

internal sealed class PageTemplateConfiguration : BaseEntityConfiguration<PageTemplate>
{
    public override void Configure(EntityTypeBuilder<PageTemplate> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Type).HasDefaultValue(PageTemplateType.Other);
        builder.Property(t => t.IsLinked).HasDefaultValue(false);

        builder.HasOne(t => t.Language)
            .WithMany()
            .HasForeignKey(t => t.LanguageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Type).HasDatabaseName("IX_PageTemplates_Type");
        builder.HasIndex(t => t.IsLinked).HasDatabaseName("IX_PageTemplates_IsLinked");
        builder.HasIndex(t => t.LanguageId).HasDatabaseName("IX_PageTemplates_LanguageId");
    }
}
