using Domain.Entities.PageInfos;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPages;

/// <summary>
/// Lists GrapeJS builder pages.
/// </summary>
/// <param name="LanguageId">Restrict to one language. Null returns every language.</param>
/// <param name="Status">Restrict to one publish status. Null returns every status.</param>
/// <param name="Kinds">
/// Page types to include. Empty means "every type" — which is also how you exclude
/// one: select everything except it. That keeps the UI a plain multi-select instead
/// of needing a separate include/exclude mode.
/// </param>
/// <param name="RequiredFlags">
/// Every bit set here must also be set on the page, so ticking two boxes narrows the
/// list instead of widening it. "Pages with no structured data that are not landing
/// pages" is <c>MissingStructuredData</c> plus every <paramref name="Kinds"/> value
/// except <see cref="PageKind.LandingPage"/>.
/// </param>
public sealed record GetPagesQuery(
    int? LanguageId = null,
    PageStatus? Status = null,
    IReadOnlyList<PageKind>? Kinds = null,
    PageSignal RequiredSignals = PageSignal.None) : IQuery<List<PageListItemResponse>>;
