using System.Text.Json.Nodes;
using Application.Features.Commands.StructuredData;
using Domain.Entities.StructuredData;
using Shouldly;

namespace Cms.Tests.StructuredData;

/// <summary>
/// Covers <see cref="SchemaNodeFactory.BuildFaq"/> against the markup the FAQ block
/// actually emits.
/// <para>
/// Pinned deliberately: there used to be two FAQ blocks — a plain accordion and a
/// near-identical "FAQ (Schema)" variant — and only the second carried these
/// markers, so an author who reached for the wrong one produced a page with no
/// FAQPage node and nothing on screen to say so. The blocks were merged into one
/// that always carries them, which changed the markup from &lt;div&gt;/&lt;h3&gt;/
/// &lt;p&gt; to &lt;details&gt;/&lt;summary&gt;/&lt;p&gt;. If that shape ever stops
/// being understood here, structured data goes quiet again in exactly the way this
/// merge existed to prevent — hence a test rather than trust.
/// </para>
/// </summary>
public sealed class SchemaNodeFactoryFaqTests
{
    private const string PageUrl = "https://example.com/sss";

    /// <summary>The markup the merged block emits, copied in shape from elevare-blocks.js.</summary>
    private const string AccordionHtml = """
        <section data-elevare-faq style="padding:80px 20px">
            <div style="max-width:800px;margin:0 auto;">
                <h2>Sıkça Sorulan Sorular</h2>
                <div class="el-faq-list">
                    <details data-elevare-faq-item style="margin-bottom:16px;">
                        <summary data-elevare-faq-question style="font-weight:600;">Kargo ne zaman gelir?</summary>
                        <p data-elevare-faq-answer style="margin-top:8px;">Siparişler iki iş günü içinde teslim edilir.</p>
                    </details>
                    <details data-elevare-faq-item style="margin-bottom:16px;">
                        <summary data-elevare-faq-question style="font-weight:600;">İade yapabilir miyim?</summary>
                        <p data-elevare-faq-answer style="margin-top:8px;">On dört gün içinde koşulsuz iade edebilirsiniz.</p>
                    </details>
                </div>
            </div>
        </section>
        """;

    [Fact]
    public void The_accordion_markup_produces_a_question_per_details_element()
    {
        JsonObject? node = SchemaNodeFactory.BuildFaq(AccordionHtml, PageUrl);

        node.ShouldNotBeNull();
        node[SchemaGraph.TypeKey]!.GetValue<string>().ShouldBe(SchemaCatalog.FaqPage.Type);

        JsonArray questions = node["mainEntity"]!.AsArray();
        questions.Count.ShouldBe(2);

        questions[0]!["name"]!.GetValue<string>().ShouldBe("Kargo ne zaman gelir?");
        questions[0]!["acceptedAnswer"]!["text"]!.GetValue<string>()
            .ShouldBe("Siparişler iki iş günü içinde teslim edilir.");
        questions[1]!["name"]!.GetValue<string>().ShouldBe("İade yapabilir miyim?");
    }

    /// <summary>
    /// A question with no answer written yet is skipped rather than emitted empty —
    /// claiming the page answers something it does not is worse than saying nothing.
    /// </summary>
    [Fact]
    public void A_half_filled_entry_is_skipped()
    {
        const string html = """
            <section data-elevare-faq>
                <details data-elevare-faq-item>
                    <summary data-elevare-faq-question>Cevabı yazılmamış soru?</summary>
                    <p data-elevare-faq-answer>   </p>
                </details>
                <details data-elevare-faq-item>
                    <summary data-elevare-faq-question>Tam olan soru?</summary>
                    <p data-elevare-faq-answer>Tam olan cevap.</p>
                </details>
            </section>
            """;

        JsonObject? node = SchemaNodeFactory.BuildFaq(html, PageUrl);

        node.ShouldNotBeNull();
        JsonArray questions = node["mainEntity"]!.AsArray();
        questions.Count.ShouldBe(1);
        questions[0]!["name"]!.GetValue<string>().ShouldBe("Tam olan soru?");
    }

    /// <summary>
    /// The generic Accordion block is the same interaction with none of the markers,
    /// precisely so collapsible content that is not a question cannot end up in the
    /// page's structured data.
    /// </summary>
    [Fact]
    public void A_plain_accordion_without_markers_produces_nothing()
    {
        const string html = """
            <section style="padding:60px 20px">
                <div class="el-acc-list">
                    <details><summary>Teslimat Bilgileri</summary><div><p>Metin.</p></div></details>
                </div>
            </section>
            """;

        SchemaNodeFactory.BuildFaq(html, PageUrl).ShouldBeNull();
    }

    [Fact]
    public void A_page_with_no_faq_block_produces_nothing()
    {
        SchemaNodeFactory.BuildFaq("<p>Sadece metin</p>", PageUrl).ShouldBeNull();
    }
}
