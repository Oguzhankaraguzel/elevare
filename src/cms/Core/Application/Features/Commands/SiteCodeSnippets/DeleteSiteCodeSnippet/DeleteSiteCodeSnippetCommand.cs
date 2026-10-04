using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.SiteCodeSnippets.DeleteSiteCodeSnippet;

public sealed record DeleteSiteCodeSnippetCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.CustomCodeAuthor;
}
