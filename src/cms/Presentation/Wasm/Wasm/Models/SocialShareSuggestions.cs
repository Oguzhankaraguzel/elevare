namespace Wasm.Models;

/// <summary>
/// What the Social Sharing panel's "Otomatik doldur" writes into an empty field, as
/// far as the page editor knows it up front — Site Settings and the page's own
/// details. What only the canvas knows (its first image, the Article block's date)
/// the panel reads from the editor itself. Nothing here reaches the public site
/// unless the author fills a field with it.
/// </summary>
/// <param name="SiteDescription">Site Settings › SEO › default meta description.</param>
/// <param name="SiteImage">Site Settings › SEO › default share image.</param>
/// <param name="XHandle">The handle in Site Settings' X profile link.</param>
/// <param name="Locale">og:locale derived from the page's language.</param>
/// <param name="PageLink">The page's public address, when the site's address is set.</param>
/// <param name="Published">The page's publish date (PageInfo.PublishedAt).</param>
/// <param name="Tags">The page's tags.</param>
public sealed record SocialShareSuggestions(
    string? SiteDescription,
    string? SiteImage,
    string? XHandle,
    string? Locale,
    string? PageLink,
    DateTime? Published,
    IReadOnlyList<string> Tags)
{
    public static readonly SocialShareSuggestions Empty = new(null, null, null, null, null, null, []);

    /// <summary>The public site's og:locale for a language code: "tr" → "tr_TR", "pt-br" → "pt_BR".</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "og:locale is lowercase language, uppercase region by definition.")]
    public static string? OgLocale(string? twoLetterCode)
    {
        if (string.IsNullOrWhiteSpace(twoLetterCode))
            return null;
        string code = twoLetterCode.Trim().ToLowerInvariant();
        int dash = code.IndexOf('-', StringComparison.Ordinal);
        if (dash > 0 && dash == code.Length - 3)
            return $"{code[..dash]}_{code[(dash + 1)..].ToUpperInvariant()}";
        return code switch
        {
            "tr" => "tr_TR",
            "en" => "en_US",
            _ => code,
        };
    }
}
