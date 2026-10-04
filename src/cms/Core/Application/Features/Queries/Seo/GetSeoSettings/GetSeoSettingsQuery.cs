using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Seo.GetSeoSettings;

/// <summary>
/// Reads only the SEO-owned settings. Deliberately not <c>GetSiteSettingsQuery</c>
/// with a group filter: that one is gated on SiteSettings.Manage, and the whole
/// point of Seo.Manage is that an editor can hold it without also being able to
/// read the mail credentials sitting in the same table.
/// </summary>
public sealed record GetSeoSettingsQuery : IQuery<SeoSettingsResponse>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SeoManage;
}
