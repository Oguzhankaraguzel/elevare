using Application.Abstraction.Data;
using Domain.Entities.PublicAnalytics;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Analytics.RecordPageDuration;

internal sealed class RecordPageDurationCommandHandler(IAnalyticsDbContext db)
    : ICommandHandler<RecordPageDurationCommand>
{
    /// <summary>Cap at 4 hours — anything longer is a tab left open, not reading time.</summary>
    private const int MaxSeconds = 4 * 60 * 60;

    public async Task<Result> Handle(RecordPageDurationCommand request, CancellationToken cancellationToken)
    {
        if (request.HitId <= 0 || request.Seconds < 0)
            return Result.Success(); // nothing to record; not worth an error response

        int seconds = Math.Min(request.Seconds, MaxSeconds);

        PublicPageViewHit? hit = await db.PageViewHits
            .FirstOrDefaultAsync(h => h.Id == request.HitId, cancellationToken);

        // Unknown id (expired/bogus beacon) is a silent no-op; keep the larger value
        // when multiple beacons arrive for the same hit (e.g. tab re-focused).
        if (hit is null || hit.DurationSeconds is int existing && existing >= seconds)
            return Result.Success();

        hit.DurationSeconds = seconds;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
