using Application.Security;
using Domain.Entities.SiteCodeSnippets;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Content;

/// <summary>
/// The consent preset's one transformation: the vendor's stylesheet stops blocking
/// first paint, the script stays exactly as pasted (it has to run before the
/// trackers it gates), and the CDN is preconnected.
/// </summary>
public sealed class CookieConsentPresetTests
{
    private const string Paste =
        "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/gh/orestbida/cookieconsent@3/dist/cookieconsent.css\">\n" +
        "<script src=\"https://cdn.jsdelivr.net/gh/orestbida/cookieconsent@3/dist/cookieconsent.umd.js\"></script>\n" +
        "<script>CookieConsent.run({});</script>";

    [Fact]
    public void Stylesheet_becomes_its_own_non_blocking_style_row()
    {
        Result<IReadOnlyList<SiteCodePart>> result = SiteCodePresetFactory.Build(SiteCodePreset.CookieConsent, Paste);

        result.IsSuccess.ShouldBeTrue();
        SiteCodePart css = result.Value.Single(p => p.Kind == SiteCodeKind.Style);
        css.Placement.ShouldBe(SiteCodePlacement.HeadStart);
        css.Content.ShouldContain("media=\"print\" onload=\"this.media='all'\"");
        css.Content.ShouldContain("<noscript><link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/gh/orestbida/cookieconsent@3/dist/cookieconsent.css\"></noscript>");
    }

    [Fact]
    public void Script_is_untouched_and_preceded_by_a_preconnect()
    {
        Result<IReadOnlyList<SiteCodePart>> result = SiteCodePresetFactory.Build(SiteCodePreset.CookieConsent, Paste);

        SiteCodePart script = result.Value.Single(p => p.Kind == SiteCodeKind.Script);
        script.Content.ShouldStartWith("<link rel=\"preconnect\" href=\"https://cdn.jsdelivr.net\" crossorigin>");
        script.Content.ShouldContain("<script src=\"https://cdn.jsdelivr.net/gh/orestbida/cookieconsent@3/dist/cookieconsent.umd.js\"></script>");
        script.Content.ShouldContain("CookieConsent.run({});");
        script.Content.ShouldNotContain("defer");
        script.Content.ShouldNotContain("rel=\"stylesheet\"");
    }

    [Fact]
    public void A_script_only_paste_is_one_row_as_before()
    {
        Result<IReadOnlyList<SiteCodePart>> result = SiteCodePresetFactory.Build(
            SiteCodePreset.CookieConsent, "<script src=\"https://cdn.cookieyes.com/client_data/x/script.js\"></script>");

        result.Value.Count.ShouldBe(1);
        result.Value[0].Kind.ShouldBe(SiteCodeKind.Script);
        result.Value[0].Content.ShouldContain("preconnect\" href=\"https://cdn.cookieyes.com\"");
    }
}
