using SharedKernel.Content;

namespace Application.Services;

/// <summary>The few words a listing writes itself, in the page's language.</summary>
internal sealed record ListingTexts(string Pagination, string PreviousPage, string NextPage, string SearchList)
{
    private static readonly ListingTexts Turkish = new("Sayfalama", "Önceki sayfa", "Sonraki sayfa", "Bu listede ara");
    private static readonly ListingTexts English = new("Pagination", "Previous page", "Next page", "Search this list");

    public static ListingTexts For(string languageCode) => IsTurkish(languageCode) ? Turkish : English;

    public static string FormatDate(DateTime date, string languageCode) => HumanDateFormatter.Format(date, languageCode);

    private static bool IsTurkish(string languageCode) =>
        languageCode.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
}
