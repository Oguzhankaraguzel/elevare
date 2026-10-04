using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Asks the public site whether it is actually serving.
/// <para>
/// The CMS and the Web app are separate processes; the CMS staying up says nothing
/// about whether visitors can reach the site. Everything else in the health check
/// reads the database, which is exactly the thing that keeps working while the
/// public site is down — so without this probe the CMS reports "all clear" during a
/// full outage. That is the one failure the indicator exists to catch.
/// </para>
/// </summary>
public interface IPublicSiteProbe
{
    Task<Result<PublicSiteStatus>> CheckAsync(CancellationToken cancellationToken = default);
}
