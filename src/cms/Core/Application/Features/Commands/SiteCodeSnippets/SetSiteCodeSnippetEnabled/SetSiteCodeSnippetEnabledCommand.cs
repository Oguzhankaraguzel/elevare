using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.SiteCodeSnippets.SetSiteCodeSnippetEnabled;

/// <summary>
/// Turns a snippet on or off without touching its content — the first move when the
/// public site starts misbehaving and the cause is not yet known.
/// </summary>
public sealed record SetSiteCodeSnippetEnabledCommand(int Id, bool IsEnabled) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.CustomCodeAuthor;
}
