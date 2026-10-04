using Application.Abstraction.Data;
using Domain.Entities.PublicAnalytics;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Analytics.RecordPageView;

internal sealed class RecordPageViewCommandHandler(IAnalyticsDbContext db)
    : ICommandHandler<RecordPageViewCommand, long>
{
    private const int MaxPathLength = 500;
    private const int MaxTitleLength = 300;

    public async Task<Result<long>> Handle(RecordPageViewCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return Result.Failure<long>(PublicAnalyticsErrors.ViewEmptyPath);

        string path = request.Path.Trim();
        if (path.Length > MaxPathLength)
            path = path[..MaxPathLength];

        string? title = request.Title?.Trim();
        if (title is { Length: > MaxTitleLength })
            title = title[..MaxTitleLength];

        var hit = new PublicPageViewHit
        {
            Path = path,
            Title = string.IsNullOrEmpty(title) ? null : title,
            VisitorId = request.VisitorId,
            ViewedAtUtc = DateTime.UtcNow
        };

        db.PageViewHits.Add(hit);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(hit.Id);
    }
}
