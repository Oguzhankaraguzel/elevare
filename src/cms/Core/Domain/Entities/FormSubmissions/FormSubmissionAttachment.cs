using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.FormSubmissions;

/// <summary>
/// A file a visitor attached to a form submission — a CV on an application form,
/// a photo on a quote request. Stored in the database, not on disk: the public
/// site writes it and the CMS reads it, and the database is the one store the two
/// already share (the CMS's upload volume is read-only to the public site, and
/// S3 is optional). It is also the only store that is private by construction —
/// nothing under <c>/uploads</c>, no URL to guess — and it leaves with its
/// submission (cascade), so the retention of one is the retention of the other.
/// <para>
/// The limits are <see cref="SharedKernel.Forms.FormAttachmentPolicy"/>'s: at most
/// three files of ten megabytes, five types, each admitted on its bytes.
/// </para>
/// </summary>
public class FormSubmissionAttachment
{
    public int Id { get; set; }

    public int FormSubmissionId { get; set; }
    public FormSubmission? FormSubmission { get; set; }

    /// <summary>Which field of the form carried it (the input's <c>name</c>).</summary>
    [MaxLength(200)]
    public required string FieldName { get; set; }

    /// <summary>The visitor's file name, cleaned, with the extension the bytes say.</summary>
    [MaxLength(255)]
    public required string FileName { get; set; }

    [MaxLength(100)]
    public required string ContentType { get; set; }

    public long Size { get; set; }

    public required byte[] Content { get; set; }
}
