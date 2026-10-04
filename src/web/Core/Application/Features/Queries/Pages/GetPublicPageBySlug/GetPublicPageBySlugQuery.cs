using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPublicPageBySlug;

/// <summary>
/// Looks up a builder page by its full slug + language, for anonymous public
/// rendering. Only <c>Published</c> pages are ever returned — Draft/Archived pages
/// are indistinguishable from "does not exist" to this query, by design.
/// </summary>
public sealed record GetPublicPageBySlugQuery(string LanguageCode, string Slug) : IQuery<PublicPageResponse>;
