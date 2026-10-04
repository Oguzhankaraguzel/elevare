namespace Application.Features.Queries.Pages.GetPageParentCandidates;

/// <summary><see cref="Level"/> is 1 for a top-level page, 2 for its children, etc.</summary>
public sealed record PageParentCandidateResponse(int Id, string Title, string FullSlug, int Level);
