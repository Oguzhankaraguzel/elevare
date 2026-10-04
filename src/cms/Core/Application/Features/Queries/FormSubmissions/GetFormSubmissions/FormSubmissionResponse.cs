namespace Application.Features.Queries.FormSubmissions.GetFormSubmissions;

public sealed record FormSubmissionResponse(
    int Id,
    int PageInfoId,
    string PageTitle,
    string PageFullSlug,
    string? FormName,
    string FieldsJson,
    DateTime SubmittedAtUtc,
    DateTime? RepliedAtUtc,
    string? RepliedByUserName,
    string? ReplyToEmail,
    string? ReplySubject,
    string? ReplyBody,
    IReadOnlyList<FormAttachmentSummary> Attachments);
