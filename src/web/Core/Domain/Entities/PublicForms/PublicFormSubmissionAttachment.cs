namespace Domain.Entities.PublicForms;

/// <summary>
/// Write-side projection of the CMS's <c>FormSubmissionAttachments</c> table: a file
/// a visitor attached to a form. The public site only ever inserts here, together
/// with the submission it belongs to; reading and downloading stay in the CMS.
/// Stored as bytes in the database because that is the one store both apps share
/// and the only one that is private without any further arrangement — see the
/// CMS entity of the same name for the reasoning.
/// </summary>
public sealed class PublicFormSubmissionAttachment
{
    public int Id { get; set; }
    public int FormSubmissionId { get; set; }
    public string FieldName { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Size { get; set; }
    public byte[] Content { get; set; } = null!;
}
