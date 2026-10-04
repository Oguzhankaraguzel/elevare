namespace Domain.Entities.PageInfos;

/// <summary>
/// What a page IS, for the people working in the CMS — a landing page reads very
/// differently from a blog post even when both are just "a published page" to the
/// database.
/// <para>
/// Deliberately never leaves the CMS: it is not rendered, not in the sitemap, and
/// not part of the public schema contract. Its whole job is to make a list of forty
/// pages scannable and to let filters ask questions like "landing pages with no
/// conversion tracking". Anything the visitor should see belongs in
/// <see cref="Domain.Entities.Tags.Tag"/> instead, which is public.
/// </para>
/// </summary>
public enum PageKind
{
    /// <summary>Not classified yet. The default, so no existing page is mislabelled.</summary>
    Unspecified = 0,

    /// <summary>A campaign or ad destination, usually short-lived and conversion-focused.</summary>
    LandingPage = 1,

    /// <summary>A hub that mostly exists to lead somewhere else (listings, indexes).</summary>
    Category = 2,

    /// <summary>Editorial content: a blog post, news item, or guide.</summary>
    Article = 3,

    /// <summary>Standing company content: about, services, contact, team.</summary>
    Corporate = 4,

    /// <summary>Legal or policy text: privacy, terms, cookie policy, KVKK.</summary>
    Legal = 5,

    /// <summary>Served by the application in response to a condition: 404, 500, maintenance.</summary>
    System = 6,
}
