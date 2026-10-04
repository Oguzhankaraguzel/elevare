using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.ContentBulkEdits.GetContentBulkEditHistory;

public sealed record GetContentBulkEditHistoryQuery : IQuery<List<ContentBulkEditHistoryItemResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.BulkEditApply;
}

