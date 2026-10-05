using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Sitemaps.GetSitemapXml;

/// <summary>
/// Reads a pre-generated sitemap XML blob from the CMS's <c>SitemapCaches</c>
/// table by cache key ("index" for /sitemap.xml, the bare section slug for
/// /sitemap-{slug}.xml). The CMS's SitemapJob (Hangfire) is what actually generates them.
/// </summary>
public sealed record GetSitemapXmlQuery(string CacheKey) : IQuery<string>;
