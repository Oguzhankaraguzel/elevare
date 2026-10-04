using System.Text.Json.Nodes;
using Domain.Entities.StructuredData;
using Shouldly;

namespace Cms.Tests.StructuredData;

/// <summary>
/// Covers <see cref="SchemaGraph"/> — the document model behind the structured
/// data builder. Its whole promise is that the stored JSON IS the editor's state,
/// so the round-trip has to be lossless even for types and properties this
/// codebase knows nothing about. A model that quietly dropped unknown keys would
/// destroy exactly the hand-written markup an advanced user cared most about.
/// </summary>
public sealed class SchemaGraphTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_stored_parses_to_an_empty_graph(string? json)
    {
        SchemaGraph.TryParse(json, out SchemaGraph graph).ShouldBeTrue();

        graph.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Malformed_json_is_reported_rather_than_thrown()
    {
        // The caller keeps the raw text and warns; it must never be handed a
        // half-parsed graph that would overwrite what the editor wrote.
        SchemaGraph.TryParse("{ not json", out _).ShouldBeFalse();
    }

    [Fact]
    public void A_full_graph_document_is_read_back()
    {
        const string json = """
            {"@context":"https://schema.org","@graph":[{"@type":"Organization","name":"Elevare"}]}
            """;

        SchemaGraph.TryParse(json, out SchemaGraph graph).ShouldBeTrue();

        graph.Nodes.Count.ShouldBe(1);
        SchemaGraph.TypeOf(graph.Nodes[0]).ShouldBe("Organization");
    }

    [Fact]
    public void A_single_bare_node_is_accepted()
    {
        // Hand-written JSON-LD is usually one object, not a graph. Rejecting it
        // would strand every page whose markup predates this builder.
        SchemaGraph.TryParse("""{"@type":"Article","headline":"Merhaba"}""", out SchemaGraph graph)
            .ShouldBeTrue();

        graph.Nodes.Count.ShouldBe(1);
        SchemaGraph.TypeOf(graph.Nodes[0]).ShouldBe("Article");
    }

    [Fact]
    public void A_bare_array_of_nodes_is_accepted()
    {
        SchemaGraph.TryParse("""[{"@type":"Person"},{"@type":"Organization"}]""", out SchemaGraph graph)
            .ShouldBeTrue();

        graph.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void Unknown_types_and_properties_survive_a_round_trip()
    {
        // The catalogue covers a curated subset on purpose; everything outside it
        // must still come back out byte-for-byte in meaning.
        const string json = """
            {"@context":"https://schema.org","@graph":[
              {"@type":"VeterinaryCare","customField":"kept","nested":{"deep":[1,2,{"deeper":true}]}}
            ]}
            """;

        SchemaGraph.TryParse(json, out SchemaGraph graph).ShouldBeTrue();
        string round = graph.Serialize();

        round.ShouldContain("VeterinaryCare");
        round.ShouldContain("customField");
        round.ShouldContain("deeper");
    }

    [Fact]
    public void Serialization_always_declares_the_schema_org_context()
    {
        // Without @context a consumer cannot tell which vocabulary the names
        // belong to, and treats the whole document as meaningless.
        var graph = SchemaGraph.CreateEmpty();
        graph.Add(new JsonObject { [SchemaGraph.TypeKey] = "WebPage" });

        graph.Serialize().ShouldContain("\"@context\": \"https://schema.org\"");
    }

    [Fact]
    public void Type_is_read_from_the_array_form_some_tools_emit()
    {
        SchemaGraph.TryParse("""{"@type":["Article","BlogPosting"]}""", out SchemaGraph graph).ShouldBeTrue();

        SchemaGraph.TypeOf(graph.Nodes[0]).ShouldBe("Article");
    }

    [Fact]
    public void Upsert_replaces_a_node_of_the_same_type_in_place()
    {
        // This is what the bulk refresh relies on: regenerate Organization from
        // Site Settings without disturbing anything the editor added around it.
        var graph = SchemaGraph.CreateEmpty();
        graph.Add(new JsonObject { [SchemaGraph.TypeKey] = "Organization", ["name"] = "Eski" });
        graph.Add(new JsonObject { [SchemaGraph.TypeKey] = "Article", ["headline"] = "Dokunulmasın" });

        graph.Upsert(new JsonObject { [SchemaGraph.TypeKey] = "Organization", ["name"] = "Yeni" });

        graph.Nodes.Count.ShouldBe(2);
        graph.FindByType("Organization")!["name"]!.ToString().ShouldBe("Yeni");
        graph.FindByType("Article")!["headline"]!.ToString().ShouldBe("Dokunulmasın");
    }

    [Fact]
    public void Upsert_appends_when_the_type_is_not_present()
    {
        var graph = SchemaGraph.CreateEmpty();
        graph.Add(new JsonObject { [SchemaGraph.TypeKey] = "Article" });

        graph.Upsert(new JsonObject { [SchemaGraph.TypeKey] = "Organization" });

        graph.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void A_node_can_be_added_twice_without_parent_conflicts()
    {
        // JsonNode allows exactly one parent; reusing an attached instance throws.
        // The graph copies on the way in so callers cannot trip over that.
        var node = new JsonObject { [SchemaGraph.TypeKey] = "Person", ["name"] = "Ada" };
        var graph = SchemaGraph.CreateEmpty();

        graph.Add(node);
        Should.NotThrow(() => graph.Add(node));

        graph.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void Reference_produces_an_id_only_pointer()
    {
        // The mechanism that stops shared data being repeated per node.
        JsonObject reference = SchemaGraph.Reference("https://site.com/#organization");

        reference.Count.ShouldBe(1);
        reference[SchemaGraph.IdKey]!.ToString().ShouldBe("https://site.com/#organization");
    }

    // ── Encoding ──────────────────────────────────────────────────────────────

    [Fact]
    public void Text_is_written_in_its_own_alphabet()
    {
        // The stock encoder escapes everything outside ASCII, which made the
        // preview unproofreadable in every language the CMS can publish in.
        SchemaGraph.TryParse(
            """{"@graph":[{"@type":"WebPage","name":"Sürüm 1.0 Notları"}]}""",
            out SchemaGraph graph).ShouldBeTrue();

        string json = graph.Serialize();

        json.ShouldContain("Sürüm 1.0 Notları");
        json.ShouldNotContain("\\u00FC");
    }

    [Fact]
    public void Plus_and_apostrophe_are_left_alone()
    {
        // Both are escaped by default for reasons that do not apply inside a JSON-LD
        // script block, and both appear constantly in real content: dialling codes
        // and Turkish possessives.
        var graph = SchemaGraph.CreateEmpty();
        graph.Add(new JsonObject
        {
            ["@type"] = "Organization",
            ["telephone"] = "+90 216 444 55 66",
            ["name"] = "Elevare'ye Hoş Geldiniz",
        });

        string json = graph.Serialize();

        json.ShouldContain("+90 216 444 55 66");
        json.ShouldContain("Elevare'ye Hoş Geldiniz");
        json.ShouldNotContain("\\u002B");
        json.ShouldNotContain("\\u0027");
    }

    [Theory]
    [InlineData("Ünlü karakterler: ğüşiöç")]   // Turkish
    [InlineData("Ελληνικά")]                    // Greek
    [InlineData("Русский")]                     // Cyrillic
    [InlineData("日本語")]                       // CJK
    [InlineData("العربية")]                     // Arabic
    public void Every_alphabet_survives_serialisation(string text)
    {
        var graph = SchemaGraph.CreateEmpty();
        graph.Add(new JsonObject { ["@type"] = "WebPage", ["name"] = text });

        graph.Serialize().ShouldContain(text);
    }

    [Fact]
    public void Script_closing_tag_cannot_survive_into_the_page()
    {
        // The graph is emitted inside <script type="application/ld+json">, and FAQ
        // text comes from page content. An unescaped </script> would end the tag
        // early and spill the rest of the graph into the document as markup.
        var graph = SchemaGraph.CreateEmpty();
        graph.Add(new JsonObject
        {
            ["@type"] = "WebPage",
            ["name"] = "</script><img src=x onerror=alert(1)>",
        });

        string json = graph.Serialize();

        json.ShouldNotContain("</script>");
        json.ShouldNotContain("<img");
    }

    // ── What the bulk refresh relies on ───────────────────────────────────────
    // It rebuilds Organization/WebSite/BreadcrumbList and Upserts them into graphs
    // an editor has since worked on. If Upsert touched anything else, a refresh
    // would silently destroy hand-entered content across every page at once.

    [Fact]
    public void Upsert_leaves_other_nodes_untouched()
    {
        SchemaGraph.TryParse(
            """
            {"@graph":[
              {"@type":"Organization","name":"Eski"},
              {"@type":"Article","headline":"Elle yazilmis","author":{"@type":"Person","name":"Ada"}}
            ]}
            """, out SchemaGraph graph).ShouldBeTrue();

        graph.Upsert(new JsonObject
        {
            [SchemaGraph.TypeKey] = "Organization",
            ["name"] = "Yeni",
        });

        graph.Nodes.Count.ShouldBe(2);
        graph.FindByType("Organization")!["name"]!.ToString().ShouldBe("Yeni");

        JsonObject article = graph.FindByType("Article")!;
        article["headline"]!.ToString().ShouldBe("Elle yazilmis");
        article["author"]!["name"]!.ToString().ShouldBe("Ada");
    }

    [Fact]
    public void Upsert_keeps_the_replaced_node_in_place()
    {
        // Order is the reading order of the emitted JSON; a refresh that shuffled
        // nodes would produce a pointless diff on every page it touched.
        SchemaGraph.TryParse(
            """
            {"@graph":[
              {"@type":"Organization","name":"O"},
              {"@type":"WebSite","name":"W"},
              {"@type":"Article","headline":"A"}
            ]}
            """, out SchemaGraph graph).ShouldBeTrue();

        graph.Upsert(new JsonObject { [SchemaGraph.TypeKey] = "WebSite", ["name"] = "W2" });

        graph.Nodes.Select(n => SchemaGraph.TypeOf(n))
            .ShouldBe(["Organization", "WebSite", "Article"]);
    }

    [Fact]
    public void Custom_fields_added_by_hand_survive_a_round_trip()
    {
        // The freeform key/value editor can store a nested object; re-serialising
        // must keep it an object rather than flattening it to a quoted string.
        SchemaGraph.TryParse(
            """
            {"@graph":[{"@type":"WebPage","mainEntity":{"@type":"Person","name":"Ada"},"custom":[1,2]}]}
            """, out SchemaGraph graph).ShouldBeTrue();

        SchemaGraph.TryParse(graph.Serialize(), out SchemaGraph again).ShouldBeTrue();

        JsonObject page = again.FindByType("WebPage")!;
        page["mainEntity"].ShouldBeOfType<JsonObject>();
        page["mainEntity"]!["name"]!.ToString().ShouldBe("Ada");
        page["custom"].ShouldBeOfType<JsonArray>();
    }

    [Fact]
    public void RemoveAt_by_type_index_drops_only_that_node()
    {
        // How the refresh clears a breadcrumb from a page that moved to top level.
        SchemaGraph.TryParse(
            """
            {"@graph":[
              {"@type":"Organization","name":"O"},
              {"@type":"BreadcrumbList","itemListElement":[]},
              {"@type":"Article","headline":"A"}
            ]}
            """, out SchemaGraph graph).ShouldBeTrue();

        graph.RemoveAt(graph.IndexOfType("BreadcrumbList"));

        graph.Nodes.Select(n => SchemaGraph.TypeOf(n)).ShouldBe(["Organization", "Article"]);
    }
}
