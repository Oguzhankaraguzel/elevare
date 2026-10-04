using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Analytics;

internal sealed class PageClickHitConfiguration : IEntityTypeConfiguration<PageClickHit>
{
    public void Configure(EntityTypeBuilder<PageClickHit> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Path).HasMaxLength(500).IsRequired();
        builder.Property(h => h.ElementLabel).HasMaxLength(200).IsRequired();

        builder.HasIndex(h => h.ClickedAtUtc).HasDatabaseName("IX_PageClickHits_ClickedAtUtc");
        builder.HasIndex(h => h.Path).HasDatabaseName("IX_PageClickHits_Path");
    }
}
