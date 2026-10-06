using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteSettings.GetSiteIdentity;

/// <summary>
/// Which site this CMS manages, for the panel's own chrome (sidebar header, browser
/// tab, sign-in screen). Deliberately not permission-gated: every signed-in user —
/// and the sign-in screen itself — needs to see which site they are about to edit.
/// </summary>
public sealed record GetSiteIdentityQuery : IQuery<SiteIdentityResponse>;
