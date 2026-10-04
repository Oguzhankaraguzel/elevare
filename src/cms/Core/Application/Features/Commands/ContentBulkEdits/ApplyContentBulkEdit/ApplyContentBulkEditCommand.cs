using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.ContentBulkEdits.ApplyContentBulkEdit;

/// <summary>
/// Replaces every occurrence of <paramref name="SearchText"/> with <paramref name="ReplaceText"/>
/// across the GrapeJS content of the given pages, snapshotting each page's prior content so the
/// whole run can be undone later via <c>RevertContentBulkEditCommand</c>.
/// </summary>
public sealed record ApplyContentBulkEditCommand(
    string SearchText,
    string ReplaceText,
    List<int> PageInfoIds) : ICommand<ApplyContentBulkEditResponse>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.BulkEditApply;
}
