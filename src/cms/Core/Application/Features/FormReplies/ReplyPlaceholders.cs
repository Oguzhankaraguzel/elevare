using System.Text.Json;
using System.Text.RegularExpressions;

namespace Application.Features.FormReplies;

/// <summary>
/// Fills <c>{{alan}}</c> placeholders in a reply template from the submission's own
/// fields, so a canned answer can still open with the visitor's name.
/// </summary>
public static partial class ReplyPlaceholders
{
    [GeneratedRegex(@"\{\{\s*(?<key>[^{}]+?)\s*\}\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Parses a submission's <c>FieldsJson</c> into a name/value map. Returns an
    /// empty map for anything unparseable — a malformed row must not stop an
    /// operator from answering the person who sent it.
    /// </summary>
    public static Dictionary<string, string> ParseFields(string? fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(fieldsJson);
            return parsed is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Replaces every <c>{{key}}</c> that matches a field name.
    /// <para>
    /// An unknown placeholder is left exactly as written rather than blanked: a
    /// visible <c>{{isim}}</c> in the compose box is a mistake the operator can see
    /// and fix before sending, whereas a silent empty gap ships "Merhaba ,".
    /// </para>
    /// </summary>
    public static string Fill(string? text, IReadOnlyDictionary<string, string> fields)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        return PlaceholderRegex().Replace(text, match =>
            fields.TryGetValue(match.Groups["key"].Value, out string? value) ? value : match.Value);
    }

    /// <summary>
    /// The submitted value most likely to be an email address — what a reply should
    /// default to. Prefers a field actually named "email"/"e-posta", then falls back
    /// to any value that looks like an address.
    /// </summary>
    public static string? GuessReplyToAddress(IReadOnlyDictionary<string, string> fields)
    {
        foreach ((string key, string value) in fields)
        {
            if (!LooksLikeEmail(value)) continue;

            string name = key.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
            if (name.Contains("mail", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("posta", StringComparison.OrdinalIgnoreCase))
                return value.Trim();
        }

        return fields.Values.FirstOrDefault(LooksLikeEmail)?.Trim();
    }

    private static bool LooksLikeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string trimmed = value.Trim();
        int at = trimmed.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at < trimmed.Length - 1
            && trimmed.IndexOf('.', at) > at + 1
            && !trimmed.Contains(' ', StringComparison.Ordinal);
    }
}
