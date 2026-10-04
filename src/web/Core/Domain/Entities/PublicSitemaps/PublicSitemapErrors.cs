using SharedKernel.Concrete;

namespace Domain.Entities.PublicSitemaps;

public static class PublicSitemapErrors
{
    public static Error NotFound(string cacheKey) =>
        Error.NotFound("Sitemap.NotFound", $"No sitemap has been generated yet for cache key '{cacheKey}'.");
}
