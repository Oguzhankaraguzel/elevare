using System.Globalization;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Files;
using Application.Security;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteSettings.UpdateSiteSetting;

internal sealed class UpdateSiteSettingCommandHandler(ICmsApplicationDbContext db, IUserContext userContext, IFaviconService favicons)
    : ICommandHandler<UpdateSiteSettingCommand>
{
    public const string FaviconUrlKey = "Appearance.FaviconUrl";
    /// <summary>
    /// Set (to a timestamp) when the sized favicon PNGs exist, cleared when the
    /// favicon is something they cannot be made from. The public layout reads it to
    /// decide between the sized set and the single link. A hidden row: nothing an
    /// operator edits.
    /// </summary>
    public const string FaviconSetVersionKey = "Appearance.FaviconSetVersion";

    public async Task<Result> Handle(UpdateSiteSettingCommand request, CancellationToken cancellationToken)
    {
        // Settings such as Analytics.CustomHeadScript/CustomBodyScript are meant to hold
        // raw <script> snippets (GTM/Analytics/Pixel) — gate them the same way as
        // page/template custom-code blocks.
        if (CustomCodeGuard.ContainsCustomCode(request.Value) && !userContext.CanAuthorCustomCode)
            return Result.Failure(SiteSettingErrors.CustomCodeNotAllowed);

        SiteSetting? setting = await db.SiteSettings
            .FirstOrDefaultAsync(s => s.Key == request.Key && !s.IsDeleted, cancellationToken);

        if (setting is null)
            return Result.Failure(SiteSettingErrors.NotFound);

        setting.Value = request.Value;
        setting.UpdateDate = DateTime.UtcNow;
        setting.UpdateUserId = request.UpdatedBy;

        // A new favicon gets its sized PNG set right away — see IFaviconService.
        if (request.Key == FaviconUrlKey)
        {
            Result<bool> generated = await favicons.RegenerateAsync(request.Value, cancellationToken);
            if (generated.IsFailure) return generated;
            await SetAsync(FaviconSetVersionKey,
                generated.Value ? DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) : null,
                request.UpdatedBy, cancellationToken);
        }

        return Result.Success();
    }

    private async Task SetAsync(string key, string? value, Guid updatedBy, CancellationToken cancellationToken)
    {
        SiteSetting? row = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted, cancellationToken);
        if (row is null) return;
        row.Value = value;
        row.UpdateDate = DateTime.UtcNow;
        row.UpdateUserId = updatedBy;
    }
}
