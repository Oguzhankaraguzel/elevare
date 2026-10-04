using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Media.DeleteMediaFile;

public sealed record DeleteMediaFileCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.MediaDelete;
}
