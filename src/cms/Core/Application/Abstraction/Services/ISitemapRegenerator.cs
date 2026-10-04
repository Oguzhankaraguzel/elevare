namespace Application.Abstraction.Services;

/// <summary>
/// Asks for the sitemaps to be rebuilt now rather than at the next scheduled run —
/// for changes that move many pages at once, such as a new default language.
/// </summary>
public interface ISitemapRegenerator
{
    /// <summary>Returns immediately; the rebuild runs shortly after, in the background.</summary>
    void RequestRegeneration();
}
