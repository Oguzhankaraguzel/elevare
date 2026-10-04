namespace Application.Abstraction.Services;

/// <summary>
/// Tells the public site that something it renders may have changed. The site keeps
/// rendered pages for a long time and relies on this to drop them — see
/// <c>PublicSiteCacheInvalidationPipelineBehavior</c>, the one caller.
/// </summary>
public interface IPublicSiteCacheInvalidator
{
    /// <summary>
    /// Returns immediately; the call to the site happens shortly after, in the
    /// background, and requests made close together are sent as one.
    /// </summary>
    void RequestInvalidation();
}
