using Domain.Entities.StructuredData;
using Shouldly;

namespace Cms.Tests.StructuredData;

/// <summary>
/// Covers <see cref="SchemaValidator"/>. The severity split is the point of these
/// tests: only genuinely unreadable output may block a save, because schema.org
/// declares no property mandatory and Google states its types have no required
/// properties — so promoting "incomplete" to "invalid" would stop editors
/// publishing content that is merely unusual.
/// </summary>
public sealed class SchemaValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_structured_data_is_not_a_problem(string? json)
    {
        // Structured data is opt-in; a page without it is a normal page.
        SchemaValidator.Validate(json).ShouldBeEmpty();
    }

    [Fact]
    public void Malformed_json_blocks_the_save()
    {
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate("{ broken");

        SchemaValidator.HasBlockingError(issues).ShouldBeTrue();
    }

    [Fact]
    public void A_node_without_a_type_blocks_the_save()
    {
        // Keys with no @type are a bag of strings, not schema.org data.
        IReadOnlyList<SchemaValidationIssue> issues =
            SchemaValidator.Validate("""{"@graph":[{"name":"tür yok"}]}""");

        SchemaValidator.HasBlockingError(issues).ShouldBeTrue();
    }

    [Fact]
    public void Missing_recommended_properties_only_warn()
    {
        IReadOnlyList<SchemaValidationIssue> issues =
            SchemaValidator.Validate("""{"@graph":[{"@type":"Article","headline":"Sadece başlık"}]}""");

        issues.ShouldNotBeEmpty();
        SchemaValidator.HasBlockingError(issues).ShouldBeFalse();
        issues.ShouldAllBe(i => i.Severity == SchemaValidationSeverity.Warning);
    }

    [Fact]
    public void An_empty_node_is_flagged_as_saying_nothing()
    {
        // A template that was added and never filled in: valid JSON, zero meaning.
        IReadOnlyList<SchemaValidationIssue> issues =
            SchemaValidator.Validate("""{"@graph":[{"@type":"Article","@id":"https://x/#a"}]}""");

        issues.ShouldContain(i => i.Severity == SchemaValidationSeverity.Warning);
        SchemaValidator.HasBlockingError(issues).ShouldBeFalse();
    }

    [Fact]
    public void Blank_values_do_not_count_as_filled_in()
    {
        // Empty strings and empty arrays serialise into the output looking like
        // answers; they must be treated as the absences they are.
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """{"@graph":[{"@type":"Organization","name":"","sameAs":[],"url":"   "}]}""");

        issues.ShouldContain(i => i.Severity == SchemaValidationSeverity.Warning);
    }

    [Fact]
    public void An_unknown_type_is_left_alone()
    {
        // The catalogue is a curated subset, not a whitelist — complaining about
        // types it happens not to cover would punish correct markup.
        IReadOnlyList<SchemaValidationIssue> issues =
            SchemaValidator.Validate("""{"@graph":[{"@type":"VeterinaryCare","name":"Klinik"}]}""");

        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Reference_only_types_are_not_asked_for_recommended_fields()
    {
        // A ListItem exists to be pointed at; judging its completeness the way a
        // page-level type is judged produces noise on every breadcrumb.
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """{"@graph":[{"@type":"ListItem","position":1}]}""");

        issues.ShouldBeEmpty();
    }

    [Fact]
    public void A_fully_filled_node_produces_no_findings()
    {
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """
            {"@graph":[{
              "@type":"Organization",
              "name":"Elevare","url":"https://elevare.com","logo":"https://elevare.com/logo.png",
              "sameAs":["https://instagram.com/elevare"]
            }]}
            """);

        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Findings_point_at_the_node_they_concern()
    {
        // Without an index the editor has to guess which of ten nodes is at fault.
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """
            {"@graph":[
              {"@type":"Organization","name":"Tam","url":"https://x.com","logo":"https://x.com/l.png","sameAs":["https://x.com/p"]},
              {"@type":"Article","headline":"Eksik"}
            ]}
            """);

        issues.ShouldNotBeEmpty();
        issues.ShouldAllBe(i => i.NodeIndex == 1);
    }

    [Fact]
    public void Reference_to_a_removed_node_is_reported()
    {
        // Removing Organization from the graph is one click, and it silently leaves
        // WebSite pointing at an id nothing defines any more.
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """
            {"@graph":[
              {"@type":"WebSite","@id":"https://x.com/#website","name":"X","url":"https://x.com",
               "publisher":{"@id":"https://x.com/#organization"}}
            ]}
            """);

        issues.ShouldContain(i => i.Code == SchemaIssueCodes.DanglingReference
                                  && i.Args.Any(a => a.Contains("#organization", StringComparison.Ordinal)));
    }

    [Fact]
    public void Reference_that_resolves_is_not_reported()
    {
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """
            {"@graph":[
              {"@type":"Organization","@id":"https://x.com/#organization",
               "name":"X","url":"https://x.com","logo":"https://x.com/l.png","sameAs":["https://x.com/p"]},
              {"@type":"WebSite","@id":"https://x.com/#website","name":"X","url":"https://x.com",
               "publisher":{"@id":"https://x.com/#organization"}}
            ]}
            """);

        issues.ShouldNotContain(i => i.Code == SchemaIssueCodes.DanglingReference);
    }

    [Fact]
    public void Node_carrying_its_own_data_alongside_an_id_is_not_a_reference()
    {
        // {"@id":..., "name":...} defines something; only a lone @id is a pointer.
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """
            {"@graph":[
              {"@type":"Article","headline":"H","author":{"@id":"https://x.com/#me","name":"Ada"},
               "datePublished":"2026-01-01","image":"https://x.com/i.png"}
            ]}
            """);

        issues.ShouldNotContain(i => i.Code == SchemaIssueCodes.DanglingReference);
    }

    [Fact]
    public void Dangling_reference_inside_an_array_is_reported()
    {
        IReadOnlyList<SchemaValidationIssue> issues = SchemaValidator.Validate(
            """
            {"@graph":[
              {"@type":"Article","headline":"H","datePublished":"2026-01-01","image":"https://x.com/i.png",
               "author":[{"@id":"https://x.com/#gone"}]}
            ]}
            """);

        issues.ShouldContain(i => i.Code == SchemaIssueCodes.DanglingReference
                                  && i.Args.Any(a => a.Contains("#gone", StringComparison.Ordinal)));
    }
}
