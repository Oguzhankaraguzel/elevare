using Application.Abstraction.Security;
using Application.Features.Queries.Tags.GetTags;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Tags.UpdateTag;

/// <summary>
/// Renames a tag. Gated on <see cref="PermissionKeys.PagesEdit"/> rather than a
/// permission of its own: a tag is page metadata, and whoever may edit the pages
/// carrying it is exactly who should be able to fix a typo in it.
/// </summary>
public sealed record UpdateTagCommand(int Id, string Name) : ICommand<TagResponse>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
