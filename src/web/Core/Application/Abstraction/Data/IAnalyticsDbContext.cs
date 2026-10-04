using Domain.Entities.PublicAnalytics;
using Domain.Entities.PublicForms;
using Domain.Entities.PublicLogs;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstraction.Data;

/// <summary>
/// Write access to the append-only tables (<c>PageViewHits</c>, <c>PageClickHits</c>,
/// <c>FormSubmissions</c>, <c>AppLogs</c>) the public Web app writes to. Kept separate
/// from <see cref="IPublicReadDbContext"/> so the read-only contract of everything else stays intact.
/// </summary>
public interface IAnalyticsDbContext
{
    DbSet<PublicPageViewHit> PageViewHits { get; }

    DbSet<PublicPageClickHit> PageClickHits { get; }

    DbSet<PublicFormSubmission> FormSubmissions { get; }

    DbSet<PublicFormSubmissionAttachment> FormSubmissionAttachments { get; }

    DbSet<PublicAppLog> AppLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
