using Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Entities.PageInfos;

internal sealed class PageInfoConfiguration : BaseEntityConfiguration<PageInfo>
{
    public override void Configure(EntityTypeBuilder<PageInfo> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Slug).HasMaxLength(50).IsRequired();
        builder.Property(p => p.FullSlug).HasMaxLength(1000).IsRequired().HasDefaultValue(string.Empty);

        // Slug must be unique per language — scoped to non-deleted rows only, so a
        // soft-deleted page's old slug can be reused by a brand-new page without
        // tripping the unique constraint (soft-deleted rows are excluded from
        // uniqueness the same way UX_Languages_Default_ActiveNotDeleted excludes them).
        builder.HasIndex(p => new { p.Slug, p.LanguageId })
            .IsUnique()
            .HasDatabaseName("UX_PageInfos_Slug_Language")
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(p => p.Slug).HasDatabaseName("IX_PageInfos_Slug");
        builder.HasIndex(p => p.FullSlug).HasDatabaseName("IX_PageInfos_FullSlug");
        builder.HasIndex(p => p.PageGroupId).HasDatabaseName("IX_PageInfos_PageGroupId");

        builder.OwnsOne(p => p.SeoMeta, seo =>
        {
            // Core meta
            seo.Property(s => s.Title).HasMaxLength(200).HasColumnName("SeoTitle");
            seo.Property(s => s.MetaDescription).HasMaxLength(500).HasColumnName("SeoMetaDescription");
            seo.Property(s => s.MetaAuthor).HasMaxLength(100).HasColumnName("SeoMetaAuthor");
            seo.Property(s => s.IsCanonical).HasColumnName("SeoIsCanonical");
            seo.Property(s => s.CanonicalUrl).HasMaxLength(500).HasColumnName("SeoCanonicalUrl");
            seo.Property(s => s.StructuredData).HasColumnName("SeoStructuredData");
            // Crawler directives — read by the public site's robots meta tag.
            seo.Property(s => s.NoIndex).HasColumnName("SeoNoIndex");
            seo.Property(s => s.NoFollow).HasColumnName("SeoNoFollow");
            seo.Property(s => s.SeoScore).HasColumnName("SeoScore");
            seo.Property(s => s.SeoScoreUpdatedAt).HasColumnName("SeoScoreUpdatedAt");
            // Open Graph
            seo.Property(s => s.OgTitle).HasMaxLength(200).HasColumnName("OgTitle");
            seo.Property(s => s.OgDescription).HasMaxLength(500).HasColumnName("OgDescription");
            seo.Property(s => s.OgType).HasMaxLength(50).HasColumnName("OgType");
            seo.Property(s => s.OgImage).HasMaxLength(500).HasColumnName("OgImage");
            seo.Property(s => s.OgUrl).HasMaxLength(500).HasColumnName("OgUrl");
            // Twitter Card
            seo.Property(s => s.TwitterCard).HasMaxLength(50).HasColumnName("TwitterCard");
            seo.Property(s => s.TwitterSite).HasMaxLength(100).HasColumnName("TwitterSite");
            // Extended social metadata, one JSON document (see SeoMeta.SocialJson).
            seo.Property(s => s.SocialJson).HasColumnName("SeoSocialJson").HasColumnType("jsonb");
        });

        builder.HasOne(p => p.ParentPage)
            .WithMany(p => p.ChildPages)
            .HasForeignKey(p => p.ParentPageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Language)
            .WithMany()
            .HasForeignKey(p => p.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
