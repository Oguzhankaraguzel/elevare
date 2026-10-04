using Application.Abstraction.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Caching;

/// <summary>
/// Sends the public site its cache-clear call in the background, folding requests
/// that arrive close together (a bulk edit, a save that runs several commands) into
/// one call.
/// </summary>
/// <remarks>
/// Singleton, so the pending flag is shared by every circuit; the clear itself runs
/// in its own scope because <see cref="ICacheClearService"/> is a typed HttpClient.
/// A failed call is only logged: the command it follows has already succeeded, and
/// the site's own cache lifetime is the fallback.
/// </remarks>
internal sealed class PublicSiteCacheInvalidator(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicSiteCacheInvalidator> logger) : IPublicSiteCacheInvalidator
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);
    private int _scheduled;

    public void RequestInvalidation()
    {
        if (Interlocked.Exchange(ref _scheduled, 1) == 1)
            return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(Debounce);
            // Reset before the call, not after: a change committed while the request
            // is in flight must schedule another one, not be swallowed by this one.
            Interlocked.Exchange(ref _scheduled, 0);
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                ICacheClearService cacheClear = scope.ServiceProvider.GetRequiredService<ICacheClearService>();
                Result result = await cacheClear.ClearAsync();
                if (result.IsFailure)
                    logger.LogWarning("Public site cache was not cleared after a change: {Error}", result.Error.Description);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Public site cache was not cleared after a change.");
            }
        });
    }
}
