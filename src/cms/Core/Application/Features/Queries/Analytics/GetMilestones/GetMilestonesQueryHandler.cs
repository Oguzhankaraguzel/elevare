using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Analytics.GetMilestones;

/// <summary>
/// Finds pages that have just passed a round view count, so the CMS can say well
/// done about it.
/// <para>
/// Deliberately derived from <c>PageViewHits</c> rather than stored in a table of
/// its own. That table is trimmed to a retention window, so a stored lifetime total
/// would drift away from the data behind it and start claiming milestones nothing
/// could confirm. Reading straight from the hits keeps the count and the crossing
/// date describing the same rows: as old hits age out, the celebration quietly
/// steps down or disappears instead of becoming a number nobody can reproduce.
/// </para>
/// </summary>
internal sealed class GetMilestonesQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetMilestonesQuery, List<MilestoneResponse>>
{
    /// <summary>
    /// Only the busiest handful are examined. Each one costs a second small query to
    /// date its crossing, and a page outside the top of the list cannot be sitting on
    /// a milestone the top of the list has not already passed.
    /// </summary>
    private const int CandidateLimit = 10;

    private static readonly int SmallestThreshold = MilestoneThresholds.All[^1];

    public async Task<Result<List<MilestoneResponse>>> Handle(
        GetMilestonesQuery request, CancellationToken cancellationToken)
    {
        var candidates = await db.PageViewHits
            .GroupBy(h => h.Path)
            .Select(g => new { Path = g.Key, Views = g.Count() })
            .Where(x => x.Views >= SmallestThreshold)
            .OrderByDescending(x => x.Views)
            .Take(CandidateLimit)
            .ToListAsync(cancellationToken);

        List<MilestoneResponse> milestones = [];
        DateTime freshSince = DateTime.UtcNow - MilestoneThresholds.FreshFor;

        foreach (var candidate in candidates)
        {
            if (MilestoneThresholds.HighestPassed(candidate.Views) is not int threshold)
                continue;

            // The crossing moment is simply when the threshold-th view arrived.
            DateTime achievedAt = await db.PageViewHits
                .Where(h => h.Path == candidate.Path)
                .OrderBy(h => h.ViewedAtUtc)
                .Skip(threshold - 1)
                .Select(h => h.ViewedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (achievedAt == default)
                continue;

            // Titles change over a page's life; the newest one is the one people
            // will recognize.
            string? title = await db.PageViewHits
                .Where(h => h.Path == candidate.Path && h.Title != null)
                .OrderByDescending(h => h.ViewedAtUtc)
                .Select(h => h.Title)
                .FirstOrDefaultAsync(cancellationToken);

            milestones.Add(new MilestoneResponse(
                candidate.Path,
                string.IsNullOrWhiteSpace(title) ? candidate.Path : title,
                threshold,
                candidate.Views,
                achievedAt,
                achievedAt >= freshSince));
        }

        // Newest celebration first — the fresh ones are the point.
        List<MilestoneResponse> newestFirst = [.. milestones.OrderByDescending(m => m.AchievedAtUtc).Take(request.Take)];
        return Result.Success(newestFirst);
    }
}
