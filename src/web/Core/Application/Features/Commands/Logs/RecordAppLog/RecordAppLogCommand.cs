using Domain.Entities.PublicLogs;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Logs.RecordAppLog;

/// <summary>Appends one server- or client-side log/error entry.</summary>
public sealed record RecordAppLogCommand(
    PublicAppLogLevel Level,
    string Message,
    string? Exception,
    PublicAppLogSource Source,
    string? Path,
    string? UserAgent) : ICommand;
