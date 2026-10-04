using SharedKernel.Content;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// The site's compiled-in maintenance screen takes its photos from wwwroot, which
/// must keep working with the database down; the seeded builder page takes Media
/// Library addresses, which the CMS editor can also load.
/// </summary>
public sealed class MaintenancePageContentTests
{
    [Fact]
    public void The_fallback_screen_uses_the_sites_own_images()
    {
        MaintenancePageContent.Html.ShouldContain("src=\"/img/maintenance-bg.jpg\"");
        MaintenancePageContent.Html.ShouldContain("src=\"/img/elevare-mark-white.png\"");
    }

    [Fact]
    public void The_seeded_page_uses_the_given_addresses()
    {
        string html = MaintenancePageContent.HtmlWith("/uploads/images/bg.jpg", "/uploads/images/mark.png");

        html.ShouldContain("src=\"/uploads/images/bg.jpg\"");
        html.ShouldContain("src=\"/uploads/images/mark.png\"");
        html.ShouldNotContain("/img/");
    }

    [Fact]
    public void Without_a_background_the_photo_is_left_out()
    {
        string html = MaintenancePageContent.HtmlWith(null, "/uploads/images/mark.png");

        html.ShouldNotContain("elv-mt-bg");
        html.ShouldContain("elv-mt-mark");
    }
}
