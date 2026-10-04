using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Concrete;
using System.Data.Common;

namespace Application.Services;

/// <summary>
/// Singleton cache backing <see cref="IMaintenanceState"/>. Defaults to disabled
/// until the first refresh; a failed refresh keeps the previous value so a
/// transient DB outage can't accidentally flip the whole site into (or out of)
/// maintenance mode.
/// </summary>
internal sealed class MaintenanceState(IServiceScopeFactory scopeFactory) : IMaintenanceState
{
    public const string SettingKey = "Advanced.MaintenanceModeEnabled";

    public bool IsEnabled { get; private set; }

    public async Task<Result> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IPublicReadDbContext db = scope.ServiceProvider.GetRequiredService<IPublicReadDbContext>();

            string? value = await db.SiteSettings
                .Where(s => s.Key == SettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken);

            IsEnabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

            return Result.Success();
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            // The previous value is deliberately left in place — see the class remarks.
            return Result.Failure(SiteStateErrors.RefreshFailed("maintenance mode", ex.Message));
        }
    }
}
