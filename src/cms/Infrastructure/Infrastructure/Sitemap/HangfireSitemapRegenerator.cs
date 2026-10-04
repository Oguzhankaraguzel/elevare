using Application.Abstraction.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Sitemap;

internal sealed class HangfireSitemapRegenerator(ILogger<HangfireSitemapRegenerator> logger) : ISitemapRegenerator
{
    // The change that asked for this is saved when its command completes, after
    // this returns; a job picked up at once could still read the old pages.
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    public void RequestRegeneration()
    {
        try
        {
            BackgroundJob.Schedule<SitemapJob>(job => job.ExecuteAsync(CancellationToken.None), Delay);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            // The scheduled run still catches up; the change itself must not fail over this.
            logger.LogWarning(ex, "Queueing a sitemap rebuild failed; the next scheduled run will pick the change up.");
        }
    }
}
