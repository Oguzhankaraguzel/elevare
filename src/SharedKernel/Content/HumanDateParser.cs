using System.Globalization;
using System.Text.RegularExpressions;

namespace SharedKernel.Content;

/// <summary>
/// Reads a calendar date out of text a person wrote — a byline's "13 Ağustos 2023",
/// "August 13, 2023", "13.08.2023" — for the places that need the date an article
/// SHOWS rather than a machine field behind it.
/// </summary>
/// <remarks>
/// It exists because the two drift apart: the Article block keeps an ISO date in its
/// <c>&lt;time datetime&gt;</c> and a written one in the text, and editing the text
/// on the canvas never touched the attribute. A page read "13 Ağustos 2023" while its
/// structured data said 2026-01-01, the block's default. Where both are present, the
/// written one is what readers — and so search engines comparing the two — see.
/// Only full dates count (day, month and year); "Ağustos 2023" is not a date.
/// </remarks>
public static partial class HumanDateParser
{
    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ocak"] = 1, ["şubat"] = 2, ["subat"] = 2, ["mart"] = 3, ["nisan"] = 4, ["mayıs"] = 5,
        ["mayis"] = 5, ["haziran"] = 6, ["temmuz"] = 7, ["ağustos"] = 8, ["agustos"] = 8,
        ["eylül"] = 9, ["eylul"] = 9, ["ekim"] = 10, ["kasım"] = 11, ["kasim"] = 11, ["aralık"] = 12,
        ["aralik"] = 12,
        ["january"] = 1, ["february"] = 2, ["march"] = 3, ["april"] = 4, ["may"] = 5, ["june"] = 6,
        ["july"] = 7, ["august"] = 8, ["september"] = 9, ["october"] = 10, ["november"] = 11,
        ["december"] = 12,
        ["jan"] = 1, ["feb"] = 2, ["mar"] = 3, ["apr"] = 4, ["jun"] = 6, ["jul"] = 7, ["aug"] = 8,
        ["sep"] = 9, ["sept"] = 9, ["oct"] = 10, ["nov"] = 11, ["dec"] = 12,
    };

    /// <summary>
    /// Finds the first full date in <paramref name="text"/>. The result is midnight
    /// UTC of that day — a written date carries no time or zone of its own.
    /// </summary>
    public static bool TryParse(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        Match m = IsoDate().Match(text);
        if (m.Success && TryBuild(m.Groups["y"].Value, m.Groups["m"].Value, m.Groups["d"].Value, out date))
            return true;

        m = DayMonthNameYear().Match(text);
        if (m.Success && Months.TryGetValue(m.Groups["mon"].Value, out int month)
            && TryBuild(m.Groups["y"].Value, month.ToString(CultureInfo.InvariantCulture), m.Groups["d"].Value, out date))
            return true;

        m = MonthNameDayYear().Match(text);
        if (m.Success && Months.TryGetValue(m.Groups["mon"].Value, out month)
            && TryBuild(m.Groups["y"].Value, month.ToString(CultureInfo.InvariantCulture), m.Groups["d"].Value, out date))
            return true;

        // Day first, as written in Turkey and most of Europe: 13.08.2023, 13/08/2023.
        m = NumericDayFirst().Match(text);
        return m.Success && TryBuild(m.Groups["y"].Value, m.Groups["m"].Value, m.Groups["d"].Value, out date);
    }

    /// <summary>
    /// The date an Article block states: the written one in its byline when that is
    /// a full date, otherwise its <c>datetime</c> attribute. Null when neither is.
    /// </summary>
    /// <param name="datetimeAttribute">The <c>&lt;time datetime&gt;</c> value.</param>
    /// <param name="writtenText">The byline's visible text — the whole line, not only the
    /// <c>&lt;time&gt;</c>, since editing tends to leave part of the date outside it.</param>
    public static DateTime? ResolveArticleDate(string? datetimeAttribute, string? writtenText)
    {
        if (TryParse(writtenText, out DateTime written))
            return written;

        return DateTime.TryParse(
                datetimeAttribute, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime stamped)
            ? stamped
            : null;
    }

    private static bool TryBuild(string year, string month, string day, out DateTime date)
    {
        date = default;
        if (!int.TryParse(year, CultureInfo.InvariantCulture, out int y)
            || !int.TryParse(month, CultureInfo.InvariantCulture, out int mo)
            || !int.TryParse(day, CultureInfo.InvariantCulture, out int d))
            return false;
        if (y < 1900 || y > 2200 || mo is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, mo))
            return false;

        date = new DateTime(y, mo, d, 0, 0, 0, DateTimeKind.Utc);
        return true;
    }

    [GeneratedRegex(@"\b(?<y>\d{4})-(?<m>\d{1,2})-(?<d>\d{1,2})\b")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b(?<d>\d{1,2})\.?\s+(?<mon>\p{L}+)\s+(?<y>\d{4})\b")]
    private static partial Regex DayMonthNameYear();

    [GeneratedRegex(@"\b(?<mon>\p{L}+)\.?\s+(?<d>\d{1,2}),?\s+(?<y>\d{4})\b")]
    private static partial Regex MonthNameDayYear();

    [GeneratedRegex(@"\b(?<d>\d{1,2})[./](?<m>\d{1,2})[./](?<y>\d{4})\b")]
    private static partial Regex NumericDayFirst();
}
