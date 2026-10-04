using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Trash.GetTrashedItems;

/// <summary>Lists every soft-deleted row across all trash-eligible entity types, newest first.</summary>
public sealed record GetTrashedItemsQuery : IQuery<List<TrashItemResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.TrashView;
}

