using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>Failures from <see cref="IPublicSiteProbe"/>.</summary>
public static class PublicSiteProbeErrors
{
    public static readonly Error BaseUrlNotConfigured =
        Error.Failure("PublicSite.BaseUrlNotConfigured", "Public site address is not set.");
}
