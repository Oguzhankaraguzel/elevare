using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.SiteCodeSnippets.MoveSiteCodeSnippet;

/// <summary>
/// Swaps a snippet with its neighbour inside the same placement.
/// <para>
/// Order is not cosmetic here: a consent banner only works if it is parsed before
/// the trackers it gates, and a tag that depends on a library must come after it.
/// </para>
/// </summary>
public sealed record MoveSiteCodeSnippetCommand(int Id, bool Up) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.CustomCodeAuthor;
}
