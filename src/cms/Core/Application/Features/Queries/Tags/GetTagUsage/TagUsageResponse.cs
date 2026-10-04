namespace Application.Features.Queries.Tags.GetTagUsage;

/// <param name="PageCount">
/// How many pages carry this tag. The number that makes a tag list actionable: a
/// count of zero is the typo somebody fixed by creating a second tag and never
/// cleaned up.
/// </param>
public sealed record TagUsageResponse(int Id, string Name, string Slug, int PageCount);
