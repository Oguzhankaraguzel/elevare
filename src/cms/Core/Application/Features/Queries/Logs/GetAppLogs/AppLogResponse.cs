using Domain.Entities.Logs;

namespace Application.Features.Queries.Logs.GetAppLogs;

public sealed record AppLogResponse(
    long Id,
    AppLogLevel Level,
    string Message,
    string? Exception,
    AppLogSource Source,
    string? Path,
    string? UserAgent,
    DateTime CreatedAtUtc);
