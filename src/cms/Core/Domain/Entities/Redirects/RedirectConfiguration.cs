using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Redirects;

internal sealed class RedirectConfiguration : BaseEntityConfiguration<Redirect>
{
    public override void Configure(EntityTypeBuilder<Redirect> builder)
    {
        base.Configure(builder);

        builder.Property(r => r.OldPath).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.NewPath).HasMaxLength(2000);

        // Filtered to live rows only, same reasoning as UX_PageInfos_Slug_Language:
        // a soft-deleted redirect must not block a new one from reusing its OldPath.
        builder.HasIndex(r => r.OldPath).IsUnique().HasDatabaseName("UX_Redirects_OldPath").HasFilter("\"IsDeleted\" = false");

        // Restrict, not Cascade/SetNull: pages are only ever soft-deleted in this app
        // (see ContentBulkEditItemConfiguration), so this FK never actually fires on
        // delete — the redirect keeps following the page via SourcePageId regardless.
        builder.HasOne(r => r.SourcePage)
            .WithMany()
            .HasForeignKey(r => r.SourcePageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
