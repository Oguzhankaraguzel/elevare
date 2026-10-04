using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteHealth.GetSiteHealth;

/// <summary>
/// Collects the site-wide conditions an operator should know about, for the
/// warning indicator in the CMS header.
/// </summary>
/// <remarks>
/// Implements <see cref="IBypassDbConcurrencyGuard"/>: this is polled every 30
/// seconds per open circuit and its handler makes outbound HTTP calls (cache
/// status, public-site probe), so it must not serialize behind — or block — every
/// other request the same circuit is making. See
/// <c>DbConcurrencyGuardPipelineBehavior</c> and <c>GetSiteHealthQueryHandler</c>.
/// </remarks>
public sealed record GetSiteHealthQuery : IQuery<SiteHealthResponse>, IBypassDbConcurrencyGuard;
