using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Tags.DeleteTag;

public sealed record DeleteTagCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
