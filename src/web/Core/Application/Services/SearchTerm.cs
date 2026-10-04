namespace Application.Services;

/// <summary>
/// Lower-cases a search term the way PostgreSQL's lower() lower-cases the column it
/// is compared with — the one place the two disagree is the Turkish dotted capital:
/// lower('İşlemler') is 'işlemler', while .NET turns "İ" into "i" plus a combining
/// dot, so "İŞLEM" found nothing. Used by the site search and the page listing.
/// </summary>
public static class SearchTerm
{
    public static string LowerLikeDatabase(string term)
    {
        ArgumentNullException.ThrowIfNull(term);
#pragma warning disable CA1308 // must agree with SQL lower(), so it lower-cases
        return term.Replace('İ', 'i').ToLowerInvariant().Replace("̇", "", StringComparison.Ordinal);
#pragma warning restore CA1308
    }
}
