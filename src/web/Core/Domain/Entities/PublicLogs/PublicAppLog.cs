namespace Domain.Entities.PublicLogs;

/// <summary>
/// Writable projection of the CMS's <c>AppLogs</c> table — server exceptions
/// (from the global exception handler middleware) and client-reported JS
/// errors, same reasoning as <see cref="PublicAnalytics.PublicPageViewHit"/>.
/// </summary>
public sealed class PublicAppLog
{
    public long Id { get; set; }
    public PublicAppLogLevel Level { get; set; }
    public string Message { get; set; } = null!;
    public string? Exception { get; set; }
    public PublicAppLogSource Source { get; set; }
    public string? Path { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
