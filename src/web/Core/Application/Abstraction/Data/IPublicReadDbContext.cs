using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicMedia;
using Domain.Entities.PublicPages;
using Domain.Entities.PublicRedirects;
using Domain.Entities.PublicSitemaps;
using Domain.Entities.PublicSiteCodeSnippets;
using Domain.Entities.PublicSiteSettings;
using Domain.Entities.PublicTags;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstraction.Data;

/// <summary>
/// Read-only view over the tables the public site needs. Implemented by the Web
/// Persistence layer against the same physical database the CMS writes to — the
/// CMS remains the only writer; this context never calls SaveChanges.
/// </summary>
public interface IPublicReadDbContext
{
    DbSet<PublicPage> PageInfos { get; }
    DbSet<PublicPageContent> PageContents { get; }
    DbSet<PublicPageTemplate> PageTemplates { get; }
    DbSet<PublicSiteSetting> SiteSettings { get; }
    DbSet<PublicSiteCodeSnippet> SiteCodeSnippets { get; }
    DbSet<PublicPageInfoSiteCodeExclusion> PageInfoSiteCodeExclusions { get; }
    DbSet<PublicLanguage> Languages { get; }
    DbSet<PublicSitemapCache> SitemapCaches { get; }
    DbSet<PublicRedirect> Redirects { get; }
    DbSet<PublicTag> Tags { get; }
    DbSet<PublicPageInfoTag> PageInfoTags { get; }

    /// <summary>
    /// The media library, read so an embedded image can be upgraded to a responsive
    /// one at render time (see <c>ResponsiveImageResolutionService</c>).
    /// </summary>
    DbSet<PublicMediaFile> MediaFiles { get; }
}
