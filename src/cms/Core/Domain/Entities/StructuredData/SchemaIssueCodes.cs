namespace Domain.Entities.StructuredData;

/// <summary>
/// Translation keys for structured-data findings. Constants rather than raw strings
/// so a typo is a compile error and the resource files can be checked against this
/// list, and kept in the domain because that is where the findings are produced.
/// </summary>
public static class SchemaIssueCodes
{
    public const string InvalidJson = "SchemaIssue_InvalidJson";
    public const string NodeMissingType = "SchemaIssue_NodeMissingType";
    public const string DanglingReference = "SchemaIssue_DanglingReference";
    public const string NodeEmpty = "SchemaIssue_NodeEmpty";
    public const string RecommendedFieldsEmpty = "SchemaIssue_RecommendedFieldsEmpty";
}
