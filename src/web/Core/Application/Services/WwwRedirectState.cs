using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Concrete;
using System.Data.Common;

namespace Application.Services;

/// <summary>
/// Singleton cache backing <see cref="IWwwRedirectState"/>. Starts enabled — the
/// behaviour the site always had — and only an explicit "false" turns it off, so a
/// missing row or a failed refresh never changes which address visitors land on.
/// </summary>
internal sealed class WwwRedirectState(IServiceScopeFactory scopeFactory) : IWwwRedirectState
{
    public const string SettingKey = "Advanced.RedirectToWww";

    public bool IsEnabled { get; private set; } = true;

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

            IsEnabled = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

            return Result.Success();
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure(SiteStateErrors.RefreshFailed("www redirect", ex.Message));
        }
    }
}
