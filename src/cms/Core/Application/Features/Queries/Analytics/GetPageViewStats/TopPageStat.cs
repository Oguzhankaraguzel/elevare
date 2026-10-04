namespace Application.Features.Queries.Analytics.GetPageViewStats;

/// <summary>One row of the "most read pages" list.</summary>
public sealed record TopPageStat(string Path, string? Title, int Views, int AvgDurationSeconds);
