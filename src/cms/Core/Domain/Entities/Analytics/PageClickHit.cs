using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Analytics;

/// <summary>
/// One tracked click on the public website (e.g. a CTA button marked
/// <c>data-track-click</c>), written by the (anonymous) Web app and read by the
/// CMS per-page stats query.
/// <para>
/// Deliberately NOT a <see cref="Abstractions.BaseEntity"/>, same reasoning as
/// <see cref="PageViewHit"/>: append-only anonymous telemetry, not content.
/// </para>
/// </summary>
public class PageClickHit
{
    public long Id { get; set; }

    /// <summary>Request path the click happened on (e.g. "/hakkimizda").</summary>
    [MaxLength(500)]
    public required string Path { get; set; }

    /// <summary>Label identifying which element was clicked (the block author's <c>data-track-click</c> value).</summary>
    [MaxLength(200)]
    public required string ElementLabel { get; set; }

    /// <summary>Anonymous per-browser visitor id (same cookie as <see cref="PageViewHit.VisitorId"/>).</summary>
    public Guid VisitorId { get; set; }

    public DateTime ClickedAtUtc { get; set; }
}
