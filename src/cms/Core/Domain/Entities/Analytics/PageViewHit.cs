using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Analytics;

/// <summary>
/// One page view on the public website, written by the (anonymous) Web app and
/// read by the CMS dashboard.
/// <para>
/// Deliberately NOT a <see cref="Abstractions.BaseEntity"/>: rows are created by
/// anonymous visitors, so the audit columns (CreateUserId FK to AspNetUsers,
/// soft-delete, etc.) don't apply. This is append-only telemetry, not content.
/// </para>
/// </summary>
public class PageViewHit
{
    public long Id { get; set; }

    /// <summary>Request path as seen by the visitor (e.g. "/hakkimizda", "/en/papers").</summary>
    [MaxLength(500)]
    public required string Path { get; set; }

    /// <summary>Document title at view time; shown on the dashboard instead of the raw path when present.</summary>
    [MaxLength(300)]
    public string? Title { get; set; }

    /// <summary>Anonymous per-browser visitor id (random GUID cookie; no personal data).</summary>
    public Guid VisitorId { get; set; }

    public DateTime ViewedAtUtc { get; set; }

    /// <summary>Seconds spent on the page, reported by a beacon when the visitor leaves. Null if never reported.</summary>
    public int? DurationSeconds { get; set; }
}
