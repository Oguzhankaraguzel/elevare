namespace Domain.Entities.StructuredData;

/// <summary>
/// How the builder should render an input for a schema property. This is a UI
/// hint only — every value ends up as JSON regardless, and the freeform editor can
/// always write something the hint did not anticipate.
/// </summary>
public enum SchemaPropertyKind
{
    /// <summary>Single-line text.</summary>
    Text = 0,

    /// <summary>Multi-line text (descriptions, article bodies).</summary>
    LongText = 1,

    /// <summary>Absolute URL.</summary>
    Url = 2,

    /// <summary>ISO-8601 date, no time part.</summary>
    Date = 3,

    /// <summary>ISO-8601 date and time with offset.</summary>
    DateTime = 4,

    /// <summary>Numeric value.</summary>
    Number = 5,

    /// <summary>List of plain strings, one per line (e.g. <c>sameAs</c>).</summary>
    TextList = 6,

    /// <summary>
    /// A nested entity whose type the editor picks from
    /// <see cref="SchemaProperty.AcceptedTypes"/> — <c>author</c> being the
    /// canonical case, where schema.org allows either Person or Organization and
    /// the choice changes what the value means.
    /// </summary>
    Entity = 7,

    /// <summary>A repeating list of nested entities (FAQ questions, breadcrumb items).</summary>
    EntityList = 8,
}
