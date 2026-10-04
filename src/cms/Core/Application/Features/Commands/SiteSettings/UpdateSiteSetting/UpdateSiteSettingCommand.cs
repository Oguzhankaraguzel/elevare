using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Security;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteSettings.UpdateSiteSetting;

public sealed record UpdateSiteSettingCommand(string Key, string? Value, Guid UpdatedBy) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SiteSettingsManage;
}
