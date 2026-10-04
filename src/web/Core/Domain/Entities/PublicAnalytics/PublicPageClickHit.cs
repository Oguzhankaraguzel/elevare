namespace Domain.Entities.PublicAnalytics;

/// <summary>
/// Writable projection of the CMS's <c>PageClickHits</c> table — click telemetry
/// (e.g. a CTA button marked <c>data-track-click</c>), same reasoning as
/// <see cref="PublicPageViewHit"/>.
/// </summary>
public sealed class PublicPageClickHit
{
    public long Id { get; set; }
    public string Path { get; set; } = null!;
    public string ElementLabel { get; set; } = null!;
    public Guid VisitorId { get; set; }
    public DateTime ClickedAtUtc { get; set; }
}
