using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.FormSubmissions.GetFormSubmissions;

public sealed record GetFormSubmissionsQuery(
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<FormSubmissionResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.FormsViewSubmissions;
}

