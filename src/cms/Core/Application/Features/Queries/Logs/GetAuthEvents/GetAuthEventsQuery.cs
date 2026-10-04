using Application.Abstraction.Security;
using Domain.Entities.Logs;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Logs.GetAuthEvents;

public sealed record GetAuthEventsQuery(
    AuthEventType? EventType = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 300) : IQuery<PagedResult<AuthEventResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.AuthLogsView;
}
