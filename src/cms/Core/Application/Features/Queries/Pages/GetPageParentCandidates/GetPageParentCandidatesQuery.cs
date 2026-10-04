using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPageParentCandidates;

/// <summary>
/// Returns pages that are valid parent candidates for the "Parent Page" picker
/// in the page editor: same language, excluding the page itself and its own
/// descendants (would create a cycle), and excluding pages already at the
/// maximum nesting level (they can't accept any more children).
/// </summary>
public sealed record GetPageParentCandidatesQuery(int LanguageId, int? ExcludePageId) : IQuery<List<PageParentCandidateResponse>>;
