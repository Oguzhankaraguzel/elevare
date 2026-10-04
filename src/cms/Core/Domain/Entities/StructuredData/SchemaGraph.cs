using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Domain.Entities.StructuredData;

/// <summary>
/// A page's JSON-LD document, held as a live <c>@graph</c> of nodes.
/// <para>
/// Built on <see cref="JsonNode"/> rather than typed classes on purpose. The
/// builder must round-trip anything an editor writes — including types and
/// properties this codebase has never heard of, and values that are themselves
/// nested JSON — so the document IS the state. Typed models would silently drop
/// whatever they did not have a property for, which is exactly the data an
/// advanced user cared enough to hand-write.
/// </para>
/// <para>
/// This also means there is no separate "builder state" to keep in sync: the JSON
/// stored on the page is parsed back into the editor on reopen, so the two can
/// never drift apart.
/// </para>
/// </summary>
public sealed class SchemaGraph
{
    // Not a configurable endpoint but the vocabulary identifier the JSON-LD spec
    // defines: consumers match this exact string to know which vocabulary the
    // document speaks. Moving it to configuration would let a deployment silently
    // emit data no crawler recognises.
#pragma warning disable S1075 // Refactor your code not to use hardcoded absolute paths or URIs
    public const string ContextUrl = "https://schema.org";
#pragma warning restore S1075

    private const string ContextKey = "@context";
    private const string GraphKey = "@graph";

    /// <summary>Node type key, exposed so callers do not re-spell the literal.</summary>
    public const string TypeKey = "@type";

    /// <summary>Node identity key, used for cross-references within the graph.</summary>
    public const string IdKey = "@id";

    private readonly JsonArray _nodes;

    private SchemaGraph(JsonArray nodes) => _nodes = nodes;

    /// <summary>Nodes in graph order. Mutating a returned node mutates the graph.</summary>
    public IReadOnlyList<JsonObject> Nodes => [.. _nodes.OfType<JsonObject>()];

    public static SchemaGraph CreateEmpty() => new([]);

    /// <summary>
    /// Reads a stored document back into a graph.
    /// <para>
    /// Accepts all three shapes seen in the wild: a full <c>@graph</c> document, a
    /// bare array of nodes, and a single node object. Hand-written JSON-LD is
    /// usually one of the latter two, and refusing it would strand editors who
    /// wrote their markup before this builder existed.
    /// </para>
    /// </summary>
    /// <returns><c>false</c> when the text is not JSON at all — the caller keeps the raw text instead.</returns>
    public static bool TryParse(string? json, out SchemaGraph graph)
    {
        graph = CreateEmpty();
        if (string.IsNullOrWhiteSpace(json))
            return true;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        switch (root)
        {
            case JsonObject obj when obj[GraphKey] is JsonArray inner:
                graph = new SchemaGraph(Detach(inner));
                return true;

            case JsonObject obj:
                graph = new SchemaGraph([Detach(obj)]);
                return true;

            case JsonArray arr:
                graph = new SchemaGraph(Detach(arr));
                return true;

            default:
                // Valid JSON, but a bare string/number is not a graph of anything.
                return false;
        }
    }

    /// <summary>
    /// Serialises to the document that gets emitted, indented so the read-only
    /// preview in the builder is legible rather than one long line.
    /// </summary>
    public string Serialize()
    {
        var document = new JsonObject
        {
            [ContextKey] = ContextUrl,
            [GraphKey] = Detach(_nodes),
        };

        return Escape(document.ToJsonString(SerializerOptions));
    }

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Escapes the one character that can break out of the element this JSON is
    /// emitted into.
    /// <para>
    /// The graph ships inside <c>&lt;script type="application/ld+json"&gt;</c>, and some
    /// of it comes from page content — an FAQ answer containing <c>&lt;/script&gt;</c>
    /// would close the tag early and put the rest of the graph into the document as
    /// markup. Escaping it here rather than at the render site means storage,
    /// preview and output are all safe from one place, and a future second consumer
    /// cannot forget to do it.
    /// </para>
    /// <para>
    /// Safe as a plain replace: JSON has no structural use for <c>&lt;</c>, so every
    /// occurrence is inside a string value, where <c><</c> means exactly the
    /// same thing.
    /// </para>
    /// </summary>
    private static string Escape(string json) => json.Replace("<", "\\u003C", StringComparison.Ordinal);


    /// <summary>True when there is nothing worth emitting.</summary>
    public bool IsEmpty => _nodes.Count == 0;

    public void Add(JsonObject node) => _nodes.Add(Detach(node));

    public void RemoveAt(int index)
    {
        if (index >= 0 && index < _nodes.Count)
            _nodes.RemoveAt(index);
    }

    /// <summary>Replaces the node at <paramref name="index"/>, keeping its position.</summary>
    public void ReplaceAt(int index, JsonObject node)
    {
        if (index < 0 || index >= _nodes.Count) return;
        _nodes[index] = Detach(node);
    }

    /// <summary>First node of the given schema type, or null.</summary>
    public JsonObject? FindByType(string type) =>
        _nodes.OfType<JsonObject>()
            .FirstOrDefault(n => string.Equals(TypeOf(n), type, StringComparison.OrdinalIgnoreCase));

    /// <summary>Index of the first node of the given type, or -1.</summary>
    public int IndexOfType(string type)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i] is JsonObject o && string.Equals(TypeOf(o), type, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Adds the node, replacing any existing node of the same type in place.
    /// Used when refreshing generated nodes (Organization, BreadcrumbList) from
    /// their source of truth without disturbing anything the editor added.
    /// </summary>
    public void Upsert(JsonObject node)
    {
        string? type = TypeOf(node);
        int existing = type is null ? -1 : IndexOfType(type);
        if (existing >= 0) ReplaceAt(existing, node);
        else Add(node);
    }

    /// <summary>Reads a node's <c>@type</c>, flattening the array form some tools emit.</summary>
    public static string? TypeOf(JsonObject node) => node[TypeKey] switch
    {
        JsonValue v => v.ToString(),
        JsonArray a => a.Count > 0 ? a[0]?.ToString() : null,
        _ => null,
    };

    /// <summary>Builds a reference to another node — the mechanism that keeps shared data in one place.</summary>
    public static JsonObject Reference(string id) => new() { [IdKey] = id };

    /// <summary>
    /// A JsonNode belongs to exactly one parent, so anything already attached must
    /// be copied before it can be placed somewhere else. Skipping this throws at
    /// runtime with a message that does not obviously point here.
    /// </summary>
    private static T Detach<T>(T node) where T : JsonNode =>
        (T)JsonNode.Parse(node.ToJsonString())!;
}
