using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.ContentBulkEdits;

internal sealed class ContentBulkEditConfiguration : BaseEntityConfiguration<ContentBulkEdit>
{
    public override void Configure(EntityTypeBuilder<ContentBulkEdit> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.SearchText).HasMaxLength(500);
        builder.Property(e => e.ReplaceText).HasMaxLength(500);
        builder.Property(e => e.Kind).HasConversion<int>();

        builder.HasIndex(e => e.IsReverted).HasDatabaseName("IX_ContentBulkEdits_IsReverted");
    }
}
