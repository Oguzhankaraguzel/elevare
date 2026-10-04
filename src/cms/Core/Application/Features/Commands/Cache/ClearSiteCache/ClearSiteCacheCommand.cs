using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Cache.ClearSiteCache;

/// <summary>Backs the "Temizle" button on <c>/admin/cache</c> — see <c>ICacheClearService</c>.</summary>
public sealed record ClearSiteCacheCommand : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.CacheManage;
}
