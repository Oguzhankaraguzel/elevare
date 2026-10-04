namespace Application.Abstraction.Services;

/// <summary>
/// What the cache layer is doing right now — surfaced to the CMS's <c>/admin/cache</c>
/// page so a misconfigured or unreachable Redis is visible in the UI instead of
/// only showing up as degraded performance in the logs.
/// </summary>
/// <param name="Provider">"Memory" or "Redis".</param>
/// <param name="Healthy">
/// False when the backend is configured but currently unusable. The site keeps
/// serving either way — see <see cref="ICacheService"/>'s fail-open contract.
/// </param>
/// <param name="Error">Diagnostic detail when <paramref name="Healthy"/> is false.</param>
/// <param name="EntryCount">Cached entries, when the provider can report it cheaply.</param>
public sealed record CacheStatus(string Provider, bool Healthy, string? Error, long? EntryCount);
