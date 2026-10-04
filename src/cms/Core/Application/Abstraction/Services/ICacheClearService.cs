using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Talks to the public site's cache admin endpoints — the CMS and Web are separate
/// processes sharing only the database, so these are HTTP calls, not in-process
/// ones. See the Web app's <c>CacheEndpoints.MapCacheEndpoints</c> for the
/// receiving side.
/// </summary>
public interface ICacheClearService
{
    Task<Result> ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Live cache health, for the CMS's <c>/admin/cache</c> page.</summary>
    Task<Result<SiteCacheStatus>> GetStatusAsync(CancellationToken cancellationToken = default);
}
