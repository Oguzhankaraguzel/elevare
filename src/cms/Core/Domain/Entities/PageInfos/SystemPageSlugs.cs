namespace Domain.Entities.PageInfos;

/// <summary>
/// Pages the site serves in response to a condition rather than to a visitor asking
/// for them. They are editable like any other page, but they are not destinations —
/// anything that enumerates the site for an outside reader should leave them out.
/// <para>
/// Mirrors <c>SystemPageProvider</c> in src/web, which resolves these same slugs when
/// rendering an error or the maintenance screen.
/// </para>
/// </summary>
public static class SystemPageSlugs
{
    public const string NotFound = "404";
    public const string ServerError = "500";
    public const string Maintenance = "maintenance";

    public static readonly string[] All = [NotFound, ServerError, Maintenance];
}
