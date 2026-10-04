using Application.Abstraction.Data;
using Domain.Entities.PublicAnalytics;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Analytics.RecordPageClick;

internal sealed class RecordPageClickCommandHandler(IAnalyticsDbContext db)
    : ICommandHandler<RecordPageClickCommand>
{
    private const int MaxPathLength = 500;
    private const int MaxElementLabelLength = 200;

    public async Task<Result> Handle(RecordPageClickCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.ElementLabel))
            return Result.Failure(PublicAnalyticsErrors.ClickMissingFields);

        string path = request.Path.Trim();
        if (path.Length > MaxPathLength)
            path = path[..MaxPathLength];

        string elementLabel = request.ElementLabel.Trim();
        if (elementLabel.Length > MaxElementLabelLength)
            elementLabel = elementLabel[..MaxElementLabelLength];

        db.PageClickHits.Add(new PublicPageClickHit
        {
            Path = path,
            ElementLabel = elementLabel,
            VisitorId = request.VisitorId,
            ClickedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
