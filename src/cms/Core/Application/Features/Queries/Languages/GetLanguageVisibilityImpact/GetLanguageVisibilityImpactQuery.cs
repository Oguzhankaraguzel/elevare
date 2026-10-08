using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Languages.GetLanguageVisibilityImpact;

/// <summary>
/// What taking a language off the site (or putting it back) would touch, for the
/// confirmation the Languages screen shows before it saves.
/// </summary>
public sealed record GetLanguageVisibilityImpactQuery(int LanguageId)
    : IQuery<LanguageVisibilityImpact>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.LanguagesManage;
}
