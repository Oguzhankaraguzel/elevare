using System.Globalization;
using System.Text.Json.Nodes;

namespace Domain.Entities.StructuredData;

/// <summary>
/// Checks a page's structured data before it is saved.
/// <para>
/// Scope is deliberately narrow. Validating the whole of schema.org is not
/// attempted: the vocabulary is enormous, changes continuously, and Google's Rich
/// Results Test is the only authority that matters in the end. What this does is
/// catch the two failures that would otherwise ship silently — output no machine
/// can parse, and output that is parseable but says nothing useful.
/// </para>
/// </summary>
public static class SchemaValidator
{
    /// <summary>
    /// Nodes that exist to be pointed at rather than to describe the page, so
    /// "this node has no recommended properties" is not a meaningful complaint
    /// about them.
    /// </summary>
    private static readonly string[] ReferenceOnlyTypes = ["ListItem", "Answer"];

    public static IReadOnlyList<SchemaValidationIssue> Validate(string? rawJson)
    {
        List<SchemaValidationIssue> issues = [];

        if (string.IsNullOrWhiteSpace(rawJson))
            return issues;

        if (!SchemaGraph.TryParse(rawJson, out SchemaGraph graph))
        {
            issues.Add(new SchemaValidationIssue(
                SchemaValidationSeverity.Error,
                SchemaIssueCodes.InvalidJson));
            return issues;
        }

        if (graph.IsEmpty)
            return issues;

        IReadOnlyList<JsonObject> nodes = graph.Nodes;

        HashSet<string> knownIds = [.. nodes
            .Select(n => n[SchemaGraph.IdKey]?.ToString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)];

        for (int i = 0; i < nodes.Count; i++)
        {
            JsonObject node = nodes[i];
            string? type = SchemaGraph.TypeOf(node);

            // Without a type a node is an anonymous bag of keys: technically JSON,
            // meaningless as schema.org data.
            if (string.IsNullOrWhiteSpace(type))
            {
                issues.Add(new SchemaValidationIssue(
                    SchemaValidationSeverity.Error,
                    SchemaIssueCodes.NodeMissingType,
                    [(i + 1).ToString(CultureInfo.InvariantCulture)],
                    i));
                continue;
            }

            foreach (string target in DanglingReferences(node, knownIds))
            {
                issues.Add(new SchemaValidationIssue(
                    SchemaValidationSeverity.Warning,
                    SchemaIssueCodes.DanglingReference,
                    [SchemaCatalog.Find(type)?.Label ?? type, target],
                    i));
            }

            if (ReferenceOnlyTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                continue;

            SchemaTemplate? template = SchemaCatalog.Find(type);
            if (template is null)
                continue; // Unknown type: preserved as-is, nothing to check it against.

            // A node carrying only its type and id describes nothing — usually a
            // template that was added and then never filled in.
            bool hasAnyContent = node
                .Where(p => p.Key is not (SchemaGraph.TypeKey or SchemaGraph.IdKey))
                .Any(p => !IsBlank(p.Value));

            if (!hasAnyContent)
            {
                issues.Add(new SchemaValidationIssue(
                    SchemaValidationSeverity.Warning,
                    SchemaIssueCodes.NodeEmpty,
                    [template.Label],
                    i));
                continue;
            }

            List<string> missing = [.. template.Properties
                .Where(p => p.Recommended && IsBlank(node[p.Name]))
                .Select(p => p.Label)];

            if (missing.Count > 0)
            {
                issues.Add(new SchemaValidationIssue(
                    SchemaValidationSeverity.Warning,
                    SchemaIssueCodes.RecommendedFieldsEmpty,
                    [template.Label, string.Join(", ", missing)],
                    i));
            }
        }

        return issues;
    }

    /// <summary>True when saving must be refused.</summary>
    public static bool HasBlockingError(IReadOnlyList<SchemaValidationIssue> issues) =>
        issues.Any(i => i.Severity == SchemaValidationSeverity.Error);

    /// <summary>
    /// Ids this node points at that no node in the graph answers to.
    /// <para>
    /// This is what removing a node costs: deleting Organization leaves WebSite's
    /// <c>publisher</c> pointing at an id nothing defines any more, and a consumer
    /// following that pointer finds nothing. The reference is still valid JSON, so
    /// nothing else here would notice.
    /// </para>
    /// </summary>
    private static IEnumerable<string> DanglingReferences(JsonObject node, HashSet<string> knownIds)
    {
        foreach (KeyValuePair<string, JsonNode?> property in node)
        {
            if (property.Key == SchemaGraph.IdKey)
                continue;

            foreach (JsonNode? candidate in Flatten(property.Value))
            {
                // A lone @id is a pointer; an object that also carries its own data
                // is a definition that merely happens to be identified.
                if (candidate is not JsonObject o || o.Count != 1)
                    continue;

                string? target = o[SchemaGraph.IdKey]?.ToString();
                if (!string.IsNullOrWhiteSpace(target) && !knownIds.Contains(target))
                    yield return target;
            }
        }
    }

    /// <summary>A property holds one value or a list of them; both are references to check.</summary>
    private static IEnumerable<JsonNode?> Flatten(JsonNode? value)
    {
        if (value is JsonArray array)
        {
            foreach (JsonNode? item in array)
                yield return item;
        }
        else
        {
            yield return value;
        }
    }

    /// <summary>
    /// Treats null, empty strings, empty arrays and empty objects alike — all of
    /// them serialise into the output as noise that claims a property was filled in.
    /// </summary>
    private static bool IsBlank(JsonNode? value) => value switch
    {
        null => true,
        JsonValue v => string.IsNullOrWhiteSpace(v.ToString()),
        JsonArray a => a.Count == 0,
        JsonObject o => o.Count == 0,
        _ => false,
    };
}
