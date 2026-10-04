using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.ContentBulkEdits;

internal sealed class ContentBulkEditItemConfiguration : BaseEntityConfiguration<ContentBulkEditItem>
{
    public override void Configure(EntityTypeBuilder<ContentBulkEditItem> builder)
    {
        base.Configure(builder);

        builder.Property(i => i.PageTitleSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(i => i.PageFullSlugSnapshot).HasMaxLength(500).IsRequired();

        builder.HasOne(i => i.ContentBulkEdit)
            .WithMany(e => e.Items)
            .HasForeignKey(i => i.ContentBulkEditId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: pages are only ever soft-deleted in this app, so
        // this FK never actually blocks a delete — it just avoids EF wiring up a
        // second cascade path onto PageInfo alongside PageContent's own.
        builder.HasOne(i => i.PageInfo)
            .WithMany()
            .HasForeignKey(i => i.PageInfoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ContentBulkEditId).HasDatabaseName("IX_ContentBulkEditItems_ContentBulkEditId");
        builder.HasIndex(i => i.PageInfoId).HasDatabaseName("IX_ContentBulkEditItems_PageInfoId");
    }
}
