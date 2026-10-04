namespace Application.Features.Queries.Analytics.GetMilestones;

public static class MilestoneThresholds
{
    /// <summary>Descending, so the first match is the biggest one a page has passed.</summary>
    public static readonly int[] All = [1_000_000, 100_000, 10_000, 1_000];

    /// <summary>How long a milestone still animates before settling into a plain trophy.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromDays(2);

    /// <summary>The largest threshold <paramref name="views"/> has passed, or null.</summary>
    public static int? HighestPassed(int views) =>
        Array.Find(All, t => views >= t) is var found && found > 0 ? found : null;
}
