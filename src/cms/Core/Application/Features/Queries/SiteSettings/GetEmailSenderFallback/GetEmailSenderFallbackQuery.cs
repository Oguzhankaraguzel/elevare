using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteSettings.GetEmailSenderFallback;

/// <summary>
/// The sender the server's configuration supplies — what mail goes out from while
/// the Email.FromAddress/FromDisplayName site settings are left empty. Shown next to
/// those fields, which would otherwise look unset on a site whose sender comes from
/// <c>.env</c>.
/// </summary>
public sealed record GetEmailSenderFallbackQuery : IQuery<EmailSenderFallbackResponse>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SiteSettingsManage;
}

public sealed record EmailSenderFallbackResponse(string? FromAddress, string? FromDisplayName);
