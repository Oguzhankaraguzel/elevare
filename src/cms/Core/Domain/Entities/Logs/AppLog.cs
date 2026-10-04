using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Logs;

/// <summary>
/// One server- or client-side error/log entry, written by the (anonymous) Web app
/// and read by the CMS log viewer.
/// <para>
/// Deliberately NOT a <see cref="Abstractions.BaseEntity"/>: rows are created by
/// anonymous visitors or unauthenticated middleware, so the audit columns
/// (CreateUserId FK to AspNetUsers, soft-delete, etc.) don't apply — same
/// reasoning as <see cref="Analytics.PageViewHit"/>. This is append-only
/// telemetry, not content.
/// </para>
/// </summary>
public class AppLog
{
    public long Id { get; set; }

    public AppLogLevel Level { get; set; }

    [MaxLength(1000)]
    public required string Message { get; set; }

    /// <summary>Full exception text (type, message, stack trace) when the log entry came from a caught exception.</summary>
    public string? Exception { get; set; }

    public AppLogSource Source { get; set; }

    /// <summary>Request path the log entry occurred on (e.g. "/hakkimizda").</summary>
    [MaxLength(500)]
    public string? Path { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
