namespace Domain.Entities.StructuredData;

/// <summary>
/// How seriously to take a validation finding.
/// <para>
/// Only two levels, and the split is deliberate: an <see cref="Error"/> means the
/// output would be unusable to a machine, and nothing else. Everything about
/// completeness is a <see cref="Warning"/>, because schema.org marks almost no
/// property as mandatory and Google states outright that its types have no
/// required properties — treating "incomplete" as "invalid" would block editors
/// from describing content honestly just because it is unusual.
/// </para>
/// </summary>
public enum SchemaValidationSeverity
{
    /// <summary>Worth fixing, but the data is still valid and publishable.</summary>
    Warning = 0,

    /// <summary>The document is malformed; saving is blocked.</summary>
    Error = 1,
}
