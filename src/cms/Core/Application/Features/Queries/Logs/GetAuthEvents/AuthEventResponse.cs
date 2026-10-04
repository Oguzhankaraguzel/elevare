using Domain.Entities.Logs;

namespace Application.Features.Queries.Logs.GetAuthEvents;

public sealed record AuthEventResponse(
    long Id,
    AuthEventType EventType,
    Guid? UserId,
    string UserNameSnapshot,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAtUtc);
