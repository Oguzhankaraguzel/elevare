using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.Sitemaps;

internal sealed class SitemapCacheConfiguration : IEntityTypeConfiguration<SitemapCache>
{
    public void Configure(EntityTypeBuilder<SitemapCache> builder)
    {
        builder.ToTable("SitemapCaches");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CacheKey).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SitemapUrl).HasMaxLength(1000).IsRequired();

        builder.HasIndex(s => s.CacheKey).IsUnique().HasDatabaseName("UX_SitemapCaches_CacheKey");
        builder.HasIndex(s => s.GeneratedAt).HasDatabaseName("IX_SitemapCaches_GeneratedAt");
    }
}
