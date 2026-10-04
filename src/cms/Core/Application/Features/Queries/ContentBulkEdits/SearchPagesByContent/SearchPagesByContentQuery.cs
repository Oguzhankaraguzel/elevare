using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.ContentBulkEdits.SearchPagesByContent;

/// <summary>Finds every page whose GrapeJS content (HTML/CSS/project JSON) contains <paramref name="SearchText"/>.</summary>
public sealed record SearchPagesByContentQuery(string SearchText) : IQuery<List<PageContentMatchResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.BulkEditApply;
}

