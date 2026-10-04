namespace Domain.Entities.StructuredData;

/// <summary>
/// One property the builder knows how to offer for a schema type.
/// </summary>
/// <param name="Name">schema.org property name, verbatim (<c>datePublished</c>).</param>
/// <param name="Label">Human label shown in the builder.</param>
/// <param name="Kind">Which input to render.</param>
/// <param name="Recommended">
/// Google recommends it for this type. Drives a warning when missing — never a
/// hard failure, because schema.org itself marks almost nothing as mandatory and
/// blocking on "recommended" would stop editors describing unusual content
/// honestly.
/// </param>
/// <param name="AcceptedTypes">
/// For <see cref="SchemaPropertyKind.Entity"/> and
/// <see cref="SchemaPropertyKind.EntityList"/>: the schema types this property
/// legitimately accepts, offered as a picker. Empty for scalar kinds.
/// </param>
/// <param name="Hint">One line explaining what the value is for, shown under the field.</param>
public sealed record SchemaProperty(
    string Name,
    string Label,
    SchemaPropertyKind Kind = SchemaPropertyKind.Text,
    bool Recommended = false,
    IReadOnlyList<string>? AcceptedTypes = null,
    string? Hint = null);
