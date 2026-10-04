using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Seo.SaveSeoSettings;

/// <summary>
/// Writes the five SEO-owned settings in one round trip. A null field means
/// "leave this one alone", so each tab of the screen can save independently
/// without carrying the other tabs' values back and forth.
/// </summary>
public sealed record SaveSeoSettingsCommand(
    string? MetaTitleSuffix = null,
    string? MetaDescription = null,
    string? OgImage = null,
    string? RobotsTxt = null,
    string? LlmsTxt = null) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SeoManage;
}
