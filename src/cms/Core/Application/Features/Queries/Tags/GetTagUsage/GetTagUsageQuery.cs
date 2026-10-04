using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Tags.GetTagUsage;

/// <summary>
/// Every tag with the number of pages carrying it. Separate from
/// <c>GetTagsQuery</c>, which the page editor's picker calls on every open and has
/// no reason to pay for a count.
/// </summary>
public sealed record GetTagUsageQuery : IQuery<List<TagUsageResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
