using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPageTranslations;

/// <summary>
/// Returns the page's <c>PageGroup</c> membership and every sibling page in that
/// group (its language alternates), for the "Language versions" panel in the
/// page editor.
/// </summary>
public sealed record GetPageTranslationsQuery(int PageId) : IQuery<PageTranslationsResponse>;
