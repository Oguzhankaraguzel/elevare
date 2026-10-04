using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using Domain.Entities.Redirects;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Redirects.GetRedirects;

/// <summary>
/// Every redirect rule with its health already worked out.
/// </summary>
/// <param name="Search">Matches either side of the rule — old path or target.</param>
/// <param name="Reason">Restrict to rules created by one cause. Null returns every cause.</param>
/// <param name="RequiredHealth">
/// Every flag set here must also be set on the rule, so ticking two boxes narrows
/// rather than widens. <see cref="RedirectHealth.None"/> means "no health filter" —
/// healthy rules have no flags, so filtering *for* healthy is not expressible here
/// and is not useful: the point of the screen is finding the broken ones.
/// </param>
public sealed record GetRedirectsQuery(
    string? Search = null,
    RedirectReason? Reason = null,
    RedirectHealth RequiredHealth = RedirectHealth.None)
    : IQuery<List<RedirectListItemResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.RedirectsManage;
}
