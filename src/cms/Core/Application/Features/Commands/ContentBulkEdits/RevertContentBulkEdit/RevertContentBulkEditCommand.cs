using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.ContentBulkEdits.RevertContentBulkEdit;

/// <summary>Restores every affected page's GrapeJS content to what it was before the given bulk edit ran.</summary>
public sealed record RevertContentBulkEditCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.BulkEditRevert;
}
