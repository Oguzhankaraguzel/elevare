using Application.Abstraction.Data;
using Domain.Entities.PublicAnalytics;
using Domain.Entities.PublicForms;
using Domain.Entities.PublicLogs;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Data;

/// <summary>
/// Append-only EF context over the CMS-owned <c>PageViewHits</c>/<c>PageClickHits</c>/
/// <c>FormSubmissions</c>/<c>AppLogs</c> tables — the tables the public Web app writes to.
/// Schema/migrations remain the CMS's job.
/// </summary>
internal sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
    : DbContext(options), IAnalyticsDbContext
{
    public DbSet<PublicPageViewHit> PageViewHits => Set<PublicPageViewHit>();

    public DbSet<PublicPageClickHit> PageClickHits => Set<PublicPageClickHit>();

    public DbSet<PublicFormSubmission> FormSubmissions => Set<PublicFormSubmission>();
    public DbSet<PublicFormSubmissionAttachment> FormSubmissionAttachments => Set<PublicFormSubmissionAttachment>();

    public DbSet<PublicAppLog> AppLogs => Set<PublicAppLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PublicPageViewHit>(b =>
        {
            b.ToTable("PageViewHits");
            b.HasKey(h => h.Id);
            b.Property(h => h.Path).HasMaxLength(500);
            b.Property(h => h.Title).HasMaxLength(300);
        });

        modelBuilder.Entity<PublicPageClickHit>(b =>
        {
            b.ToTable("PageClickHits");
            b.HasKey(h => h.Id);
            b.Property(h => h.Path).HasMaxLength(500);
            b.Property(h => h.ElementLabel).HasMaxLength(200);
        });

        modelBuilder.Entity<PublicFormSubmission>(b =>
        {
            b.ToTable("FormSubmissions");
            b.HasKey(f => f.Id);
            b.Property(f => f.FormName).HasMaxLength(200);
        });

        modelBuilder.Entity<PublicFormSubmissionAttachment>(b =>
        {
            b.ToTable("FormSubmissionAttachments");
            b.HasKey(a => a.Id);
            b.Property(a => a.FieldName).HasMaxLength(200);
            b.Property(a => a.FileName).HasMaxLength(255);
            b.Property(a => a.ContentType).HasMaxLength(100);
        });

        modelBuilder.Entity<PublicAppLog>(b =>
        {
            b.ToTable("AppLogs");
            b.HasKey(l => l.Id);
            b.Property(l => l.Message).HasMaxLength(1000);
            b.Property(l => l.Path).HasMaxLength(500);
            b.Property(l => l.UserAgent).HasMaxLength(500);
        });
    }
}
