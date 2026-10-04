namespace Application.Features.Commands.StructuredData;

/// <summary>
/// The page-level facts an Article node is filled from, beyond the block itself —
/// all read from the page's settings, so nothing here has to be typed twice.
/// </summary>
/// <param name="BaseUrl">The public site's address; also the publisher's id.</param>
/// <param name="MetaDescription">The page's own meta description.</param>
/// <param name="ShareImage">See <see cref="SchemaNodeFactory.ShareImage"/>.</param>
/// <param name="DateModified">The page's modification date: the moment the draft is built — it
/// is built while the page is being edited, and saving it makes that the page's
/// last update anyway.</param>
/// <param name="Language">The page's two-letter language code.</param>
/// <param name="Keywords">The page's tags.</param>
/// <param name="Media">Media-library entries for the images on the page.</param>
/// <param name="DatePublished">When the page was published; today for a page that
/// has not been yet. An Article's byline date comes first.</param>
internal sealed record ArticleContext(
    string BaseUrl,
    string? MetaDescription,
    string? ShareImage,
    DateTime? DateModified,
    string? Language,
    IReadOnlyList<string> Keywords,
    IReadOnlyDictionary<string, MediaInfo> Media,
    DateTime? DatePublished = null);
