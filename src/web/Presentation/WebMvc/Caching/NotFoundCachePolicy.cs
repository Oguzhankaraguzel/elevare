using Microsoft.AspNetCore.OutputCaching;

namespace WebMvc.Caching;

/// <summary>
/// Lets the page policy keep a 404 for a few minutes. The built-in policy stores
/// only 200s, so every probe for a missing address — bots try hundreds — rendered
/// the 404 page from scratch. Short, because a page may be published at that
/// address; the CMS evicts <c>PageCacheTags.NotFound</c> on any page change anyway.
/// </summary>
internal sealed class NotFoundCachePolicy : IOutputCachePolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        HttpResponse response = context.HttpContext.Response;
        // The built-in policy's other refusal — a response setting a cookie is
        // someone's own — still stands.
        if (response.StatusCode == StatusCodes.Status404NotFound && response.Headers.SetCookie.Count == 0)
        {
            context.AllowCacheStorage = true;
            context.ResponseExpirationTimeSpan = Lifetime;
        }
        return ValueTask.CompletedTask;
    }
}
