using System.Globalization;

namespace SharedKernel.Content;

/// <summary>
/// Writes a date the way a reader of the page's language expects it — "13 Ağustos
/// 2023", "August 13, 2023". The public site's listing cards and the CMS editor's
/// preview of them use this, so the two read the same.
/// </summary>
public static class HumanDateFormatter
{
    private static readonly string[] TurkishMonths =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    private static readonly string[] EnglishMonths =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>
    /// Month names for the two site languages are spelled out here, so the result
    /// never depends on the host's ICU data; other languages use their culture where
    /// the server has it, else ISO 8601.
    /// </summary>
    public static string Format(DateTime date, string? languageCode)
    {
        string code = languageCode ?? "";
        if (code.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
            return $"{date.Day} {TurkishMonths[date.Month - 1]} {date.Year}";
        if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase) || code.Length == 0)
            return $"{EnglishMonths[date.Month - 1]} {date.Day}, {date.Year}";
        try
        {
            return date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo(code));
        }
        catch (CultureNotFoundException)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
