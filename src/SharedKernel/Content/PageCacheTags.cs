using System.Globalization;

namespace SharedKernel.Content;

/// <summary>
/// The output-cache tags the public site puts on a rendered page, one per thing the
/// page was built from — and the names the CMS asks it to evict after a change.
/// Shared so the two sides cannot spell a tag differently.
/// </summary>
/// <remarks>
/// A page carries <see cref="All"/>, its own <see cref="Page"/> tag, a
/// <see cref="Template"/> tag per linked template it resolved, <see cref="PageInfo"/>
/// tags for the other pages whose title or address it shows (breadcrumb, language
/// switcher), a <see cref="Group"/> tag per translation group it looked up, and
/// <see cref="Listing"/> / <see cref="Media"/> when it lists pages or shows uploaded
/// images. Evicting <see cref="All"/> is the safe answer to any change the CMS
/// cannot place.
/// </remarks>
public static class PageCacheTags
{
    /// <summary>Every cached response: pages, the 404 page, feeds and sitemaps.</summary>
    public const string All = "pages";

    /// <summary>Pages with a page listing, previous/next links or related posts — and the feeds and sitemaps.</summary>
    public const string Listing = "listing";

    /// <summary>Pages showing at least one uploaded image.</summary>
    public const string Media = "media";

    /// <summary>Cached 404 answers: a page published at that address must replace them at once.</summary>
    public const string NotFound = "notfound";

    /// <summary>The sitemaps, rebuilt by the CMS's sitemap job.</summary>
    public const string Sitemap = "sitemap";

    /// <summary>The page itself — its content and settings.</summary>
    public static string Page(int pageId) => "page:" + pageId.ToString(CultureInfo.InvariantCulture);

    /// <summary>A page whose title or address appears on another page.</summary>
    public static string PageInfo(int pageId) => "info:" + pageId.ToString(CultureInfo.InvariantCulture);

    /// <summary>A translation group: the language switcher of each of its pages links the others.</summary>
    public static string Group(int groupId) => "group:" + groupId.ToString(CultureInfo.InvariantCulture);

    /// <summary>A linked template: editing it changes every page that uses it.</summary>
    public static string Template(int templateId) => "tpl:" + templateId.ToString(CultureInfo.InvariantCulture);
}
