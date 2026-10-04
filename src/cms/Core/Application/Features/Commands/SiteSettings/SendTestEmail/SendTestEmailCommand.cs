using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.SiteSettings.SendTestEmail;

/// <summary>
/// Sends one throwaway message through the configured SMTP server so an operator can
/// find out whether e-mail works BEFORE a visitor's form submission or a password
/// reset depends on it. Until this existed, the only way to learn that the SMTP host,
/// port, credentials or From address were wrong was for a real notification to
/// silently never arrive.
/// </summary>
public sealed record SendTestEmailCommand(string ToAddress) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SiteSettingsManage;
}
