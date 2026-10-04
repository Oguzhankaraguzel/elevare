using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicLogs;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Logs.RecordAppLog;

internal sealed class RecordAppLogCommandHandler(
    IAnalyticsDbContext db,
    IErrorWebhookForwarder errorWebhookForwarder,
    ILogger<RecordAppLogCommandHandler> logger)
    : ICommandHandler<RecordAppLogCommand>
{
    private const int MaxMessageLength = 1000;
    private const int MaxPathLength = 500;
    private const int MaxUserAgentLength = 500;

    public async Task<Result> Handle(RecordAppLogCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Result.Failure(PublicAppLogErrors.MissingMessage);

        string message = request.Message.Trim();
        if (message.Length > MaxMessageLength)
            message = message[..MaxMessageLength];

        string? path = request.Path?.Trim();
        if (path is { Length: > MaxPathLength })
            path = path[..MaxPathLength];

        string? userAgent = request.UserAgent?.Trim();
        if (userAgent is { Length: > MaxUserAgentLength })
            userAgent = userAgent[..MaxUserAgentLength];

        db.AppLogs.Add(new PublicAppLog
        {
            Level = request.Level,
            Message = message,
            Exception = request.Exception,
            Source = request.Source,
            Path = string.IsNullOrWhiteSpace(path) ? null : path,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        if (request.Level == PublicAppLogLevel.Error)
        {
            Result forwarded = await errorWebhookForwarder.ForwardAsync(
                message, request.Exception, path, cancellationToken);

            // The AppLogs row above is the record of truth and it is already saved, so a
            // failed forward does not fail the command — but it is no longer invisible.
            if (forwarded.IsFailure)
                logger.LogWarning(
                    "Error was logged locally but not forwarded to the webhook: {Error}",
                    forwarded.Error.Description);
        }

        return Result.Success();
    }
}
