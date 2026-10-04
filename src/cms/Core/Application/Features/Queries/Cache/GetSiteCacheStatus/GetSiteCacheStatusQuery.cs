using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Cache.GetSiteCacheStatus;

/// <summary>Backs the health panel on <c>/admin/cache</c> — see <c>ICacheClearService</c>.</summary>
public sealed record GetSiteCacheStatusQuery : IQuery<SiteCacheStatus>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.CacheManage;
}

