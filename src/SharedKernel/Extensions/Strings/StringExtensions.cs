using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SharedKernel.Extensions.Strings;

/// <summary>
/// Provides extension methods for <see cref="string"/> operations.
/// </summary>
public static partial class StringExtensions
{
    /// <summary>
    /// Determines whether the string is <c>null</c>, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>true</c> if the string is <c>null</c>, empty, or whitespace; otherwise, <c>false</c>.</returns>
    public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Determines whether the string has a value — i.e. is not <c>null</c>, empty, or whitespace.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>true</c> if the string contains at least one non-whitespace character; otherwise, <c>false</c>.</returns>
    public static bool HasValue(this string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Converts the string to a URL-friendly slug by lowercasing, replacing spaces with hyphens,
    /// normalizing Unicode characters, and removing any characters that are not alphanumeric or hyphens.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A lowercase, hyphen-separated slug string.</returns>
    public static string ToSlug(this string value)
    {
        if (value.IsNullOrWhiteSpace())
            return string.Empty;

        string normalized = value.Normalize(NormalizationForm.FormD);

        var slugBuilder = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(c))
            {
                slugBuilder.Append('-');
                continue;
            }

            char lower = char.ToLowerInvariant(c);
            if (StandaloneLetters.TryGetValue(lower, out string? ascii))
                slugBuilder.Append(ascii);
            else
                slugBuilder.Append(lower);
        }

        string cleaned = SlugCleanupRegex().Replace(slugBuilder.ToString(), string.Empty);
        return SeparatorCollapseRegex().Replace(cleaned, "-").Trim('-');
    }

    /// <summary>
    /// Letters that FormD does not decompose, mapped to their ASCII reading.
    /// <para>
    /// Everything else survives <see cref="ToSlug"/> because FormD splits it into a
    /// base letter plus a combining mark, and dropping the mark leaves the letter.
    /// These are single code points with no decomposition, so the cleanup regex used
    /// to delete them outright: Turkish "Işık" became "isk" and German "Straße"
    /// became "strae" — silently, in the URL, with no way for the author to tell.
    /// </para>
    /// <para>
    /// Keyed on the lowercase form; <see cref="ToSlug"/> lowercases before the lookup,
    /// so the capitals are covered without a second set of entries. Turkish capital
    /// "İ" is absent on purpose: FormD does decompose it (I + combining dot above).
    /// </para>
    /// </summary>
    private static readonly Dictionary<char, string> StandaloneLetters = new()
    {
        ['ı'] = "i",    // Turkish dotless i — the one that started this
        ['ß'] = "ss",   // German sharp s
        ['ø'] = "o",    // Danish/Norwegian
        ['ł'] = "l",    // Polish
        ['đ'] = "d",    // Croatian/Serbian, Vietnamese
        ['ð'] = "d",    // Icelandic eth
        ['þ'] = "th",   // Icelandic thorn
        ['æ'] = "ae",   // Latin ligature
        ['œ'] = "oe",   // Latin ligature
        ['ŋ'] = "n",    // Sami
        ['ħ'] = "h",    // Maltese
    };

    // ── Compiled Regex ────────────────────────────────────────────────────────

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex SlugCleanupRegex();

    // Each space became its own hyphen before this, and dropped punctuation left the
    // hyphens on either side of it standing — so "  boşluklu  başlık  " or "42 — %100"
    // came out with a run of two or three in a row instead of one.
    [GeneratedRegex(@"-{2,}")]
    private static partial Regex SeparatorCollapseRegex();

}
