using Application.Features.Queries.Pages.GetPublicPageBySlug;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPreviewPageById;

/// <summary>
/// Looks up a builder page by id for a signed preview link — unlike
/// <see cref="GetPublicPageBySlugQuery"/>, this intentionally does NOT filter by
/// <c>PageStatus</c> or <c>IsActive</c>; access control is entirely the caller's
/// job (validating the signed token before ever sending this query).
/// </summary>
public sealed record GetPreviewPageByIdQuery(int PageId) : IQuery<PublicPageResponse>;
