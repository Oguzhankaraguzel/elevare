namespace Domain.Entities.StructuredData;

/// <summary>One finding from validating a page's structured data.</summary>
/// <param name="Severity">Whether this blocks saving or merely warns.</param>
/// <param name="Code">
/// Translation key, resolved against <c>CmsMessages</c> at display time. It used to
/// be a finished Turkish sentence, which meant the domain layer decided what
/// language the editor read — and an English-speaking editor got Turkish SEO advice.
/// </param>
/// <param name="Args">Values substituted into the message, in order.</param>
/// <param name="NodeIndex">Which graph node it concerns; null for document-level findings.</param>
public sealed record SchemaValidationIssue(
    SchemaValidationSeverity Severity,
    string Code,
    IReadOnlyList<string> Args,
    int? NodeIndex = null)
{
    /// <summary>Convenience for the findings that take no substitutions.</summary>
    public SchemaValidationIssue(SchemaValidationSeverity severity, string code, int? nodeIndex = null)
        : this(severity, code, [], nodeIndex)
    {
    }
}
