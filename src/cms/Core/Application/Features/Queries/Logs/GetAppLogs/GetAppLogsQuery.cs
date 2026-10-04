using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using Domain.Entities.Logs;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Logs.GetAppLogs;

public sealed record GetAppLogsQuery(
    AppLogLevel? Level = null,
    AppLogSource? Source = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 300) : IQuery<PagedResult<AppLogResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.LogsView;
}

