using System.Collections.Frozen;
using System.Security.Claims;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Permissions;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;
using System.Data.Common;

namespace Infrastructure.Authentication;

/// <summary>
/// Default <see cref="IRolePermissionCache"/>. Holds one immutable snapshot that
/// readers swap in atomically, so lookups need no locking and a concurrent refresh
/// can never expose a half-built map.
/// <para>
/// Registered as a singleton and seeded by <c>RolePermissionCacheWarmup</c> at
/// startup. Resolves <see cref="RoleManager{AppRole}"/> through a scope factory
/// because RoleManager is scoped and this service is not.
/// </para>
/// </summary>
internal sealed class RolePermissionCache : IRolePermissionCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RolePermissionCache> _logger;

    // Role name → granted permission keys. Replaced wholesale on refresh; never
    // mutated in place. Role names are compared case-insensitively to match
    // ClaimsPrincipal.IsInRole / Identity's normalised role names.
    private volatile FrozenDictionary<string, FrozenSet<string>> _snapshot =
        FrozenDictionary<string, FrozenSet<string>>.Empty;

    public RolePermissionCache(IServiceScopeFactory scopeFactory, ILogger<RolePermissionCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsGranted(IEnumerable<string> roleNames, string permissionKey)
    {
        FrozenDictionary<string, FrozenSet<string>> snapshot = _snapshot;
        foreach (string roleName in roleNames)
        {
            if (snapshot.TryGetValue(roleName, out FrozenSet<string>? permissions)
                && permissions.Contains(permissionKey))
                return true;
        }

        return false;
    }

    public async Task<Result> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            RoleManager<AppRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

            List<AppRole> roles = await roleManager.Roles.ToListAsync(cancellationToken);
            Dictionary<string, FrozenSet<string>> map = new(StringComparer.OrdinalIgnoreCase);

            foreach (AppRole role in roles)
            {
                if (string.IsNullOrEmpty(role.Name))
                    continue;

                IList<Claim> claims = await roleManager.GetClaimsAsync(role);
                map[role.Name] = claims
                    .Where(c => c.Type == PermissionKeys.ClaimType)
                    .Select(c => c.Value)
                    .ToFrozenSet(StringComparer.Ordinal);
            }

            // Swapped in only after the whole map is built, so a failure part-way
            // through leaves the previous (correct) snapshot serving lookups rather
            // than a partial one that would under-grant permissions.
            _snapshot = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("Role permission cache refreshed for {RoleCount} role(s).", map.Count);

            return Result.Success();
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or OperationCanceledException)
        {
            _logger.LogError(ex, "Refreshing the role permission cache failed.");
            return Result.Failure(RolePermissionErrors.RefreshFailed(ex.Message));
        }
    }
}
