namespace Domain.Entities.PublicAnalytics;

/// <summary>
/// Writable projection of the CMS's <c>PageViewHits</c> table — the ONE table the
/// public Web app is allowed to write to (append-only telemetry created by anonymous
/// visitors; everything else remains read-only, owned and migrated by the CMS).
/// </summary>
public sealed class PublicPageViewHit
{
    public long Id { get; set; }
    public string Path { get; set; } = null!;
    public string? Title { get; set; }
    public Guid VisitorId { get; set; }
    public DateTime ViewedAtUtc { get; set; }
    public int? DurationSeconds { get; set; }
}
