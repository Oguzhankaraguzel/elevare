namespace Domain.Entities.StructuredData;

/// <summary>
/// A schema.org type the builder can add to a page's graph, together with the
/// properties worth offering for it.
/// </summary>
/// <param name="Type">schema.org type name (<c>Article</c>), used verbatim as <c>@type</c>.</param>
/// <param name="Label">Human name shown in the "add node" picker.</param>
/// <param name="Description">One line on when this type is the right choice.</param>
/// <param name="Properties">Properties offered as ready-made fields.</param>
/// <param name="IdFragment">
/// Fragment appended to the page URL to form this node's <c>@id</c>
/// (<c>#article</c>). Nodes with an id can be referenced from elsewhere in the
/// graph instead of being repeated — which is the whole reason the output is a
/// graph rather than a pile of separate scripts. Null for nodes that are always
/// inline (a nested Question, for example).
/// </param>
/// <param name="Note">
/// Something the editor should know before choosing this type — a deprecated
/// rich result, say. Surfaced in the picker rather than buried in documentation
/// nobody opens.
/// </param>
public sealed record SchemaTemplate(
    string Type,
    string Label,
    string Description,
    IReadOnlyList<SchemaProperty> Properties,
    string? IdFragment = null,
    string? Note = null);
