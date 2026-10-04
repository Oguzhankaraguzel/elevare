using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.FormSubmissions;

/// <summary>
/// One visitor submission of a GrapeJS-authored form, written by the
/// (anonymous) Web app and read by the CMS.
/// <para>
/// Deliberately NOT a <see cref="Abstractions.BaseEntity"/>: rows are created
/// by anonymous visitors, so the audit columns (CreateUserId FK to
/// AspNetUsers, soft-delete, etc.) don't apply — same reasoning as
/// <see cref="Analytics.PageViewHit"/>.
/// </para>
/// Field shapes are fully dynamic (the editor designs whatever fields they
/// want), so submitted values are stored as a JSON object rather than a
/// fixed column schema.
/// </summary>
public class FormSubmission
{
    public int Id { get; set; }

    public int PageInfoId { get; set; }
    public PageInfos.PageInfo? PageInfo { get; set; }

    /// <summary>The form's own "Form Adı" trait value, if set — lets the same page host multiple named forms.</summary>
    [MaxLength(200)]
    public string? FormName { get; set; }

    /// <summary>Submitted field name/value pairs, serialized as JSON.</summary>
    public required string FieldsJson { get; set; }

    public DateTime SubmittedAtUtc { get; set; }

    /// <summary>
    /// When someone replied from the CMS, and what they actually sent.
    /// <para>
    /// The sent text is stored rather than just the template id: templates get
    /// edited, and "what did we tell this person" must not silently change
    /// afterwards. A reply is also allowed to be edited before sending, so the
    /// template alone would not be the truth anyway.
    /// </para>
    /// All nullable, so the Web app's writable projection is unaffected — it never
    /// touches these columns.
    /// </summary>
    public DateTime? RepliedAtUtc { get; set; }

    public Guid? RepliedByUserId { get; set; }
    public Users.AppUser? RepliedByUser { get; set; }

    [MaxLength(256)]
    public string? ReplyToEmail { get; set; }

    [MaxLength(300)]
    public string? ReplySubject { get; set; }

    public string? ReplyBody { get; set; }

    /// <summary>Files the visitor attached; see <see cref="FormSubmissionAttachment"/>.</summary>
    public ICollection<FormSubmissionAttachment> Attachments { get; set; } = [];
}
