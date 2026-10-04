using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Analytics;

internal sealed class PageViewHitConfiguration : IEntityTypeConfiguration<PageViewHit>
{
    public void Configure(EntityTypeBuilder<PageViewHit> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Path).HasMaxLength(500).IsRequired();
        builder.Property(h => h.Title).HasMaxLength(300);

        // The dashboard always filters by date range, then groups by path.
        builder.HasIndex(h => h.ViewedAtUtc).HasDatabaseName("IX_PageViewHits_ViewedAtUtc");
        builder.HasIndex(h => h.Path).HasDatabaseName("IX_PageViewHits_Path");
    }
}
