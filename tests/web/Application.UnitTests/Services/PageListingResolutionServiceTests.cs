using Application.Services;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers <see cref="PageListingResolutionService.BuildPageNumberSequence"/> — the
/// pure page-number-sequence algorithm behind the "Sayfa Listesi" block's pagination
/// (independent from the AngleSharp DOM cloning, which needs a live page/tag dataset
/// to exercise meaningfully and is covered by live browser verification instead).
/// </summary>
public sealed class PageListingResolutionServiceTests
{
    [Theory]
    [InlineData("numeric")]
    [InlineData("numeric-arrows")]
    public void Numeric_styles_return_every_page_number(string style)
    {
        List<(int? Page, bool IsEllipsis)> sequence = PageListingResolutionService.BuildPageNumberSequence(5, 3, style);

        sequence.ShouldBe([(1, false), (2, false), (3, false), (4, false), (5, false)]);
    }

    [Fact]
    public void Truncated_style_windows_around_the_current_page_with_ellipsis()
    {
        List<(int? Page, bool IsEllipsis)> sequence = PageListingResolutionService.BuildPageNumberSequence(10, 5, "truncated");

        sequence.ShouldBe([(1, false), (null, true), (4, false), (5, false), (6, false), (null, true), (10, false)]);
    }

    [Fact]
    public void Truncated_style_has_no_ellipsis_when_current_page_is_near_the_start()
    {
        List<(int? Page, bool IsEllipsis)> sequence = PageListingResolutionService.BuildPageNumberSequence(10, 1, "truncated");

        sequence.ShouldBe([(1, false), (2, false), (null, true), (10, false)]);
    }

    [Fact]
    public void Truncated_style_collapses_to_a_single_run_when_total_pages_is_small()
    {
        List<(int? Page, bool IsEllipsis)> sequence = PageListingResolutionService.BuildPageNumberSequence(3, 2, "truncated");

        sequence.ShouldBe([(1, false), (2, false), (3, false)]);
    }

    [Fact]
    public void Truncated_arrows_style_windows_exactly_like_truncated()
    {
        List<(int? Page, bool IsEllipsis)> sequence = PageListingResolutionService.BuildPageNumberSequence(10, 5, "truncated-arrows");

        sequence.ShouldBe(PageListingResolutionService.BuildPageNumberSequence(10, 5, "truncated"));
    }

    [Fact]
    public void Prev_next_style_renders_no_page_numbers_at_all()
    {
        List<(int? Page, bool IsEllipsis)> sequence = PageListingResolutionService.BuildPageNumberSequence(10, 5, "prev-next");

        sequence.ShouldBeEmpty();
    }
}
