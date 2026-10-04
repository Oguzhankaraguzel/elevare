using Application.Abstraction.Data;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicMedia;
using Domain.Entities.PublicPages;
using Domain.Entities.PublicRedirects;
using Domain.Entities.PublicSitemaps;
using Domain.Entities.PublicSiteCodeSnippets;
using Domain.Entities.PublicSiteSettings;
using Domain.Entities.PublicTags;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Data;

/// <summary>
/// Read-only EF Core context over the same physical database the CMS owns and
/// migrates. Deliberately maps only the columns the public site needs (no
/// navigation to AppUser/Identity, no writes, no migrations) — the CMS remains
/// the single writer and schema owner; this context is never used to SaveChanges.
/// </summary>
internal sealed class PublicReadDbContext(DbContextOptions<PublicReadDbContext> options)
    : DbContext(options), IPublicReadDbContext
{
    public DbSet<PublicPage> PageInfos => Set<PublicPage>();
    public DbSet<PublicPageContent> PageContents => Set<PublicPageContent>();
    public DbSet<PublicPageTemplate> PageTemplates => Set<PublicPageTemplate>();
    public DbSet<PublicSiteSetting> SiteSettings => Set<PublicSiteSetting>();
    public DbSet<PublicSiteCodeSnippet> SiteCodeSnippets => Set<PublicSiteCodeSnippet>();
    public DbSet<PublicPageInfoSiteCodeExclusion> PageInfoSiteCodeExclusions => Set<PublicPageInfoSiteCodeExclusion>();
    public DbSet<PublicLanguage> Languages => Set<PublicLanguage>();
    public DbSet<PublicSitemapCache> SitemapCaches => Set<PublicSitemapCache>();
    public DbSet<PublicRedirect> Redirects => Set<PublicRedirect>();
    public DbSet<PublicTag> Tags => Set<PublicTag>();
    public DbSet<PublicPageInfoTag> PageInfoTags => Set<PublicPageInfoTag>();
    public DbSet<PublicMediaFile> MediaFiles => Set<PublicMediaFile>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        // This context never writes; skip change tracking for every query.
        => optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PublicPage>(b =>
        {
            b.ToTable("PageInfos");
            b.HasKey(p => p.Id);
            b.Property(p => p.PageStatus).HasColumnName("PageStatus");
            b.Property(p => p.SeoSocialJson).HasColumnType("jsonb");
            b.HasQueryFilter(p => !p.IsDeleted);

            b.HasOne(p => p.Content)
                .WithOne()
                .HasForeignKey<PublicPageContent>(c => c.PageInfoId);
        });

        modelBuilder.Entity<PublicPageContent>(b =>
        {
            b.ToTable("PageContents");
            b.HasKey(c => c.Id);
            b.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<PublicPageTemplate>(b =>
        {
            b.ToTable("PageTemplates");
            b.HasKey(t => t.Id);
            b.HasQueryFilter(t => !t.IsDeleted);
        });

        modelBuilder.Entity<PublicSiteSetting>(b =>
        {
            b.ToTable("SiteSettings");
            b.HasKey(s => s.Id);
            b.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<PublicSiteCodeSnippet>(b =>
        {
            b.ToTable("SiteCodeSnippets");
            b.HasKey(s => s.Id);
            b.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<PublicLanguage>(b =>
        {
            b.ToTable("Languages");
            b.HasKey(l => l.Id);
            b.HasQueryFilter(l => !l.IsDeleted);
        });

        modelBuilder.Entity<PublicSitemapCache>(b =>
        {
            b.ToTable("SitemapCaches");
            b.HasKey(s => s.Id);
        });

        modelBuilder.Entity<PublicRedirect>(b =>
        {
            b.ToTable("Redirects");
            b.HasKey(r => r.Id);
            b.HasQueryFilter(r => !r.IsDeleted);
        });

        modelBuilder.Entity<PublicTag>(b =>
        {
            b.ToTable("Tags");
            b.HasKey(t => t.Id);
            b.HasQueryFilter(t => !t.IsDeleted);
        });

        modelBuilder.Entity<PublicPageInfoTag>(b =>
        {
            b.ToTable("PageInfoTags");
            b.HasKey(pt => new { pt.PageInfoId, pt.TagId });
        });

        modelBuilder.Entity<PublicPageInfoSiteCodeExclusion>(b =>
        {
            b.ToTable("PageInfoSiteCodeExclusions");
            b.HasKey(x => new { x.PageInfoId, x.SiteCodeSnippetId });
        });

        modelBuilder.Entity<PublicMediaFile>(b =>
        {
            b.ToTable("MediaFiles");
            b.HasKey(m => m.Id);
            b.HasQueryFilter(m => !m.IsDeleted);
        });
    }
}
