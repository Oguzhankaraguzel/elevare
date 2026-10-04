namespace Application.Features.Queries.Analytics.GetMilestones;

/// <param name="Threshold">The round number that was passed — 1.000, 10.000, …</param>
/// <param name="IsFresh">
/// Crossed recently enough to still be worth animating. After that the card keeps
/// the trophy and goes quiet, so the CMS never ends up permanently confettied.
/// </param>
public sealed record MilestoneResponse(
    string Path,
    string Title,
    int Threshold,
    int Views,
    DateTime AchievedAtUtc,
    bool IsFresh);
