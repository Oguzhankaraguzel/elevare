namespace Infrastructure.Caching;

/// <summary>
/// Bound from <c>appsettings.json → Cache</c>.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Approximate entry-count budget for the in-process memory cache, used only
    /// when Redis isn't configured. <c>IMemoryCache</c>'s <c>SizeLimit</c> has no
    /// built-in unit — this treats it as a count (each entry weighs 1) derived
    /// once at startup from ~25% of the runtime's available memory, assuming an
    /// average cached value of <see cref="AssumedAverageEntryBytes"/> — a rough
    /// budget, not a precise byte limit (precise byte-accounting would require
    /// serializing every value just to measure it, which defeats the point of an
    /// in-process cache). Set explicitly to override the computed default.
    /// </summary>
    public long? MemorySizeLimitEntries { get; init; }

    public const long AssumedAverageEntryBytes = 50 * 1024;

    /// <summary>
    /// Shared secret the CMS sends when calling <c>POST /api/cache/clear</c> (as
    /// header <c>X-Cache-Secret</c>) — must match the CMS's own <c>Cache:ClearSecret</c>.
    /// The endpoint rejects the request when this is unset.
    /// </summary>
    public string ClearSecret { get; init; } = "";
}
