using Application.Security;
using Shouldly;

namespace Cms.Tests.Security;

/// <summary>
/// Pins what may be served as robots.txt. Only one mistake is refused — a file with
/// no User-agent line, which parses cleanly and does nothing — because everything
/// else on this list is something a site legitimately wants on some domain.
/// </summary>
public sealed class RobotsTxtValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Empty_means_use_the_built_in_default(string? content)
        => RobotsTxtValidator.Validate(content).IsValid.ShouldBeTrue();

    [Fact]
    public void An_ordinary_file_is_accepted_without_warnings()
    {
        RobotsTxtValidationResult result = RobotsTxtValidator.Validate(
            "# our rules\nUser-agent: *\nDisallow: /api/\nAllow: /api/public/\nSitemap: https://x.test/sitemap.xml");

        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Rules_with_no_user_agent_above_them_are_refused()
        => RobotsTxtValidator.Validate("Disallow: /admin/\nDisallow: /api/")
            .IsValid.ShouldBeFalse();

    [Fact]
    public void Blocking_the_whole_site_saves_but_warns()
    {
        RobotsTxtValidationResult result = RobotsTxtValidator.Validate("User-agent: *\nDisallow: /");

        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain("BlocksEntireSite");
    }

    [Fact]
    public void Blocking_one_named_crawler_is_not_the_site_wide_warning()
    {
        RobotsTxtValidationResult result =
            RobotsTxtValidator.Validate("User-agent: BadBot\nDisallow: /\n\nUser-agent: *\nDisallow: /api/");

        result.Warnings.ShouldNotContain("BlocksEntireSite");
    }

    [Fact]
    public void A_misspelled_directive_is_flagged_rather_than_silently_ignored()
    {
        RobotsTxtValidationResult result = RobotsTxtValidator.Validate("User-agent: *\nDisalow: /api/");

        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.StartsWith("UnknownDirective:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_line_with_no_colon_is_flagged()
    {
        RobotsTxtValidationResult result = RobotsTxtValidator.Validate("User-agent: *\nthis is just prose");

        result.Warnings.ShouldContain(w => w.StartsWith("UnparsableLine:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_file_too_long_for_crawlers_to_finish_is_refused()
    {
        string oversized = "User-agent: *\n" + string.Concat(
            Enumerable.Repeat("Disallow: /padding/\n", RobotsTxtValidator.MaxContentLength / 20 + 1));

        RobotsTxtValidator.Validate(oversized).IsValid.ShouldBeFalse();
    }
}
