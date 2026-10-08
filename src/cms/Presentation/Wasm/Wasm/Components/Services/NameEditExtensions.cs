using SpectraUtils.Abstract;

namespace Wasm.Components.Services;

/// <summary>
/// The word-by-word way to call SpectraUtils' name corrections.
/// <para>
/// Its single-string overloads remove every space before correcting, so a second
/// given name or a double surname was merged into one word ("ali ozan" became
/// "Aliozan", "özkul yılmaz" became "ÖZKULYILMAZ"). The array overloads keep the
/// words apart, which is what a person's name needs.
/// </para>
/// </summary>
public static class NameEditExtensions
{
    private static readonly char[] Separators = [' ', '\t', '\r', '\n'];

    /// <summary>"ali  ozan " → "Ali Ozan".</summary>
    public static string? CorrectGivenNames(this INameEdit nameEdit, string? value)
    {
        string[] words = Words(value);
        return words.Length == 0 ? value : nameEdit.NameCorrection(words);
    }

    /// <summary>"özkul yılmaz" → "ÖZKUL YILMAZ".</summary>
    public static string? CorrectSurnames(this INameEdit nameEdit, string? value)
    {
        string[] words = Words(value);
        return words.Length == 0 ? value : nameEdit.SirNameCorrection(words);
    }

    /// <summary>
    /// A user name from a person's names, with no spaces: Identity rejects a user
    /// name containing one, and <c>CreateUserName</c> keeps whatever spaces it is given.
    /// </summary>
    public static string CreateUserNameFrom(this INameEdit nameEdit, string? firstName, string? lastName) =>
        nameEdit.CreateUserName(string.Concat(Words($"{firstName} {lastName}")));

    private static string[] Words(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
}
