using SharedKernel.Extensions.Strings;
using Shouldly;

namespace Cms.Tests.Content;

/// <summary>
/// Slugs are the site's URLs, so a letter quietly vanishing from one is a broken,
/// unreadable address that nobody spots until it is already published and linked.
/// <para>
/// <c>ToSlug</c> strips accents by decomposing with FormD and dropping the combining
/// marks, which handles most of Latin script. It does nothing for letters that have
/// no decomposition — they fell through to the cleanup regex and were deleted
/// outright. Turkish is the worst affected, because dotless "ı" is everywhere:
/// "Işık" came out as "isk".
/// </para>
/// </summary>
public sealed class SlugGenerationTests
{
    [Theory]
    // The one that started this — every Turkish lowercase letter in one title.
    [InlineData("Işık Çiçeği Ürünlerimiz", "isik-cicegi-urunlerimiz")]
    [InlineData("Kırmızı Şarap", "kirmizi-sarap")]
    [InlineData("Sıkça Sorulan Sorular", "sikca-sorulan-sorular")]
    // Capital İ decomposes (I + combining dot above), so it was never broken —
    // pinned so a future rewrite of the mapping cannot regress it.
    [InlineData("İstanbul Ofisimiz", "istanbul-ofisimiz")]
    [InlineData("Bağlantı Ayarları", "baglanti-ayarlari")]
    public void A_Turkish_title_keeps_every_letter(string title, string expected) =>
        title.ToSlug().ShouldBe(expected);

    [Theory]
    // Same class of bug in other languages: single code points with no decomposition.
    [InlineData("Straße", "strasse")]
    [InlineData("Køkken", "kokken")]
    [InlineData("Łódź", "lodz")]
    [InlineData("Æther", "aether")]
    public void A_letter_without_a_decomposition_is_transliterated_not_dropped(string title, string expected) =>
        title.ToSlug().ShouldBe(expected);

    [Theory]
    [InlineData("Crème Brûlée", "creme-brulee")]
    [InlineData("Hello World", "hello-world")]
    [InlineData("tek boşluk", "tek-bosluk")]
    public void Accents_punctuation_and_spacing_still_behave(string title, string expected) =>
        title.ToSlug().ShouldBe(expected);

    [Theory]
    // Each space becomes its own hyphen, and dropped punctuation leaves the hyphens on
    // either side of it standing — without collapsing, "  boşluklu  başlık  " came out
    // "bosluklu--baslik" and "Ürün #42 — %100" came out "urun-42--100-indirim".
    [InlineData("  boşluklu  başlık  ", "bosluklu-baslik")]
    [InlineData("Ürün #42 — %100 İndirim!", "urun-42-100-indirim")]
    [InlineData("çok---fazla----tire", "cok-fazla-tire")]
    public void Repeated_separators_collapse_to_one(string title, string expected) =>
        title.ToSlug().ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("---")]
    public void A_title_with_nothing_sluggable_produces_an_empty_slug(string title) =>
        title.ToSlug().ShouldBeEmpty();

    [Fact]
    public void A_slug_never_starts_or_ends_with_a_separator() =>
        "  —Işık—  ".ToSlug().ShouldBe("isik");
}
