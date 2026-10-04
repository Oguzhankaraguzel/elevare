namespace Domain.Entities.PublicForms;

/// <summary>
/// Writable projection of the CMS's <c>FormSubmissions</c> table — one of the
/// tables the public Web app is allowed to write to (visitor-submitted form
/// data, appended anonymously; everything else remains read-only, owned and
/// migrated by the CMS).
/// </summary>
public sealed class PublicFormSubmission
{
    public int Id { get; set; }
    public int PageInfoId { get; set; }
    public string? FormName { get; set; }
    public string FieldsJson { get; set; } = null!;
    public DateTime SubmittedAtUtc { get; set; }
}
