using Domain.Entities.PageInfos;
using SharedKernel.Social;

namespace Application.Features.Queries.Pages.GetPageById;

/// <summary>Fields edited in the page builder's SEO Settings panel.</summary>
// CanonicalUrl/OgUrl are intentionally string: the domain-layer SeoMeta entity
// also stores these fields as string (see SeoMeta.cs); making the DTO a Uri would
// just add conversion overhead and wouldn't bind directly with Blazor's @bind.
#pragma warning disable CA1054
public sealed record SeoMetaResponse(
    bool IsCanonical,
    string? CanonicalUrl,
    string MetaDescription,
    string MetaAuthor,
    bool NoIndex,
    bool NoFollow,
    string? FocusKeyword,
    string? StructuredData,
    string OgTitle,
    string OgDescription,
    string OgType,
    string? OgImage,
    string? OgUrl,
    string? TwitterCard,
    string? TwitterSite,
    SocialMeta Social);
#pragma warning restore CA1054
