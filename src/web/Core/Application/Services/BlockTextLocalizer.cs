using AngleSharp.Dom;

namespace Application.Services;

/// <summary>
/// Puts the page builder's own default wording into the page's language: the
/// search box's "Ara…", a listing's "Sonuç bulunamadı.", the code block's
/// "Kopyala", screen-reader labels such as "Önceki slayt". A block carries these in
/// whatever language the editor was in when it was placed — an English page could
/// end up with Turkish ones — so they are set here, per page, on every render.
/// <para>
/// Only OUR defaults are touched: a value is replaced only when it is still exactly
/// one of the builder's own Turkish or English wordings. Anything the author wrote
/// themselves is left as it is. Languages other than Turkish get the English text.
/// </para>
/// </summary>
public static class BlockTextLocalizer
{
    private sealed record Pair(string Tr, string En);

    private static readonly string[] Attributes = ["aria-label", "placeholder", "title", "data-copied-text", "data-elevare-copied-text", "data-elevare-breadcrumb-home"];

    // Attribute values the blocks ship with — accessible names, placeholders, the
    // "copied" confirmation. Matched wherever they appear.
    private static readonly Pair[] AttributeTexts =
    [
        new("Kapat", "Close"),
        new("İçindekiler", "Table of contents"),
        new("Önceki slayt", "Previous slide"),
        new("Sonraki slayt", "Next slide"),
        new("Yazarın X Profili", "Author's X profile"),
        new("Yazarın LinkedIn Profili", "Author's LinkedIn profile"),
        new("Yan panel", "Side panel"),
        new("Videoyu oynat", "Play video"),
        new("Sitede ara", "Search the site"),
        new("Bu listede ara", "Search this list"),
        new("Sayfa Yolu", "Breadcrumb"),
        new("Mobil menüyü aç", "Open the menu"),
        new("Karşılaştırma sürgüsü", "Comparison slider"),
        new("Google Play'den Alın", "Get it on Google Play"),
        new("App Store'dan İndirin", "Download on the App Store"),
        new("Ekip üyesi X profili", "Team member's X profile"),
        new("Ekip üyesi LinkedIn profili", "Team member's LinkedIn profile"),
        new("Ekip üyesi GitHub profili", "Team member's GitHub profile"),
        new("Duyuruyu gizle", "Dismiss the announcement"),
        new("Ana Navigasyon", "Main navigation"),
        new("5 üzerinden 5 yıldız", "5 out of 5 stars"),
        new("Dil seçimi", "Language selection"),
        new("Duyuru", "Announcement"),
        new("Karanlık / aydınlık tema", "Dark / light theme"),
        new("Kategori menüsü", "Category menu"),
        new("Sosyal medya", "Social media"),
        new("WhatsApp ile yazın", "Message us on WhatsApp"),
        new("Ara...", "Search..."),
        new("Arama...", "Search..."),
        new("Kodu kopyala", "Copy code"),
        new("Kopyalandı", "Copied"),
        new("Bağlantıyı kopyala", "Copy link"),
        new("Paylaş", "Share"),
        new("Bu sayfayı paylaş", "Share this page"),
        new("X ile paylaş", "Share on X"),
        new("LinkedIn ile paylaş", "Share on LinkedIn"),
        new("Facebook ile paylaş", "Share on Facebook"),
        new("WhatsApp ile paylaş", "Share on WhatsApp"),
        new("Telegram ile paylaş", "Share on Telegram"),
        new("E-posta ile paylaş", "Share by email"),
        new("E-posta ile paylaş", "Share on Email"),
        new("Sayfanın başına dön", "Back to top"),
        new("Yazılar arasında gezinme", "Post navigation"),
        new("Takvime ekle", "Add to calendar"),
        new("Ana Sayfa", "Home"),
        new("E-posta adresiniz", "Your email address"),
    ];

    // Visible text the blocks ship with, only where the block puts it — a "Tümü" an
    // author typed into a paragraph is theirs.
    private static readonly (string Selector, Pair Text)[] ElementTexts =
    [
        ("[data-elevare-search-empty-template], [data-elevare-empty-template]", new("Sonuç bulunamadı.", "No results found.")),
        (".elevare-listing-search button", new("Ara", "Search")),
        ("[data-elevare-tag-all]", new("Tümü", "All")),
        ("[data-elevare-code-copy]", new("Kopyala", "Copy")),
        (".el-share-label", new("Paylaş:", "Share:")),
        ("[data-elevare-adjacent=prev] .el-adj-label", new("← Önceki yazı", "← Previous post")),
        ("[data-elevare-adjacent=next] .el-adj-label", new("Sonraki yazı →", "Next post →")),
        ("[data-elevare-block=elevare-preferred-source] .el-btn-label", new("Google'da tercih edilen kaynak olarak ekleyin", "Add us as a preferred source on Google")),
        ("[data-elevare-block=elevare-google-news] .el-btn-label", new("Google Haberler'de takip edin", "Follow us on Google News")),
        ("[data-elevare-block=elevare-google-review] .el-btn-label", new("Bizi Google'da değerlendirin", "Review us on Google")),
        ("[data-elevare-block=elevare-directions] .el-btn-label", new("Yol tarifi al", "Get directions")),
        ("[data-elevare-block=elevare-youtube-subscribe] .el-btn-label", new("YouTube'da abone olun", "Subscribe on YouTube")),
        ("[data-el-cal=google] .el-btn-label", new("Google Takvim", "Google Calendar")),
        ("[data-elevare-block=elevare-newsletter] .el-nl-title", new("Gelişmelerden Haberdar Olun", "Stay in the Loop")),
        ("[data-elevare-block=elevare-newsletter] .el-nl-text", new("En son haberler ve güncellemeler için bültenimize kayıt olun.", "Sign up for our newsletter to get the latest news and updates.")),
        ("[data-elevare-block=elevare-newsletter] .el-nl-btn", new("Abone Ol", "Subscribe")),
        ("[data-elevare-block=elevare-newsletter] .el-nl-consent-text", new("Bülten e-postalarını almayı kabul ediyorum; istediğim zaman ayrılabilirim.", "I agree to receive the newsletter and can unsubscribe at any time.")),
        ("[data-elevare-block=elevare-newsletter] .el-nl-privacy", new("Ayrıntılar", "Details")),
    ];

    private static readonly Dictionary<string, Pair> ByValue = BuildIndex();

    private static Dictionary<string, Pair> BuildIndex()
    {
        Dictionary<string, Pair> index = new(StringComparer.Ordinal);
        foreach (Pair pair in AttributeTexts)
        {
            index.TryAdd(pair.Tr, pair);
            index.TryAdd(pair.En, pair);
        }
        return index;
    }

    /// <summary>True when <paramref name="html"/> may hold anything this rewrites.</summary>
    public static bool MayApply(string html) =>
        html.Contains("data-elevare-", StringComparison.Ordinal) || html.Contains("el-share-label", StringComparison.Ordinal);

    public static void Apply(IDocument document, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool turkish = languageCode.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        string Pick(Pair pair) => turkish ? pair.Tr : pair.En;

        foreach (string attribute in Attributes)
        {
            foreach (IElement element in document.QuerySelectorAll($"[{attribute}]"))
            {
                string? value = element.GetAttribute(attribute);
                if (value is not null && ByValue.TryGetValue(value.Trim(), out Pair? pair))
                    element.SetAttribute(attribute, Pick(pair));
            }
        }

        foreach ((string selector, Pair text) in ElementTexts)
        {
            foreach (IElement element in document.QuerySelectorAll(selector))
            {
                // Only an element that is just our text — never one holding markup.
                if (element.Children.Length > 0) continue;
                string current = element.TextContent.Trim();
                if (current == text.Tr || current == text.En)
                    element.TextContent = Pick(text);
            }
        }
    }
}
