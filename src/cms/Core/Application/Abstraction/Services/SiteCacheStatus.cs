namespace Application.Abstraction.Services;

/// <summary>
/// The public site's cache health, as reported by its <c>/api/cache/status</c>
/// endpoint. Mirrors the Web app's own <c>CacheStatus</c> shape — an independent
/// copy, since the CMS has no compile-time dependency on the Web module (same
/// reasoning as the Public* read-model mirrors).
/// </summary>
/// <param name="Provider">"Memory" or "Redis".</param>
/// <param name="Healthy">False when the backend is configured but unusable.</param>
/// <param name="Error">Diagnostic detail to show the operator when unhealthy.</param>
/// <param name="EntryCount">Cached entries, when the provider can report it.</param>
public sealed record SiteCacheStatus(string Provider, bool Healthy, string? Error, long? EntryCount);
