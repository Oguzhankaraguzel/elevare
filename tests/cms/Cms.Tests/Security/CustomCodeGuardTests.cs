using Application.Security;
using Shouldly;

namespace Cms.Tests.Security;

/// <summary>
/// Pins the behavior of the raw-script detector that gates page/template/site-setting
/// content behind the Developer/Admin/SuperAdmin roles. It is a conservative
/// heuristic: over-flagging suspicious markup is acceptable, missing a script is not.
/// </summary>
public sealed class CustomCodeGuardTests
{
    [Theory]
    [InlineData("""<script>alert(1)</script>""")]
    [InlineData("""<SCRIPT src="https://cdn.example.com/x.js"></SCRIPT>""")]
    [InlineData("""<div><script type="module">import x from 'y';</script></div>""")]
    public void Script_tags_are_detected(string html)
        => CustomCodeGuard.ContainsCustomCode(html).ShouldBeTrue();

    [Theory]
    [InlineData("""<img src="x" onerror="alert(1)">""")]
    [InlineData("""<body onload = "run()">""")]
    [InlineData("""<a href="#" OnClick="doIt()">tıkla</a>""")]
    public void Inline_event_handlers_are_detected(string html)
        => CustomCodeGuard.ContainsCustomCode(html).ShouldBeTrue();

    [Theory]
    [InlineData("""<a href="javascript:alert(1)">tıkla</a>""")]
    [InlineData("""<iframe src=" javascript:void(0)"></iframe>""")]
    public void Javascript_protocol_links_are_detected(string html)
        => CustomCodeGuard.ContainsCustomCode(html).ShouldBeTrue();

    [Theory]
    [InlineData("""<h1>Merhaba</h1><p>Sıradan içerik.</p>""")]
    [InlineData("""<a href="/iletisim" class="btn">İletişim</a>""")]
    [InlineData("""<img src="/uploads/logo.png" alt="logo">""")]
    [InlineData("""<div data-online="1">çevrimiçi</div>""")]
    public void Plain_content_is_not_flagged(string html)
        => CustomCodeGuard.ContainsCustomCode(html).ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_input_is_not_flagged(string? html)
        => CustomCodeGuard.ContainsCustomCode(html).ShouldBeFalse();
}
