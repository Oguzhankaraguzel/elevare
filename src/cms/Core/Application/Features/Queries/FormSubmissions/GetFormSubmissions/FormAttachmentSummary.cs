namespace Application.Features.Queries.FormSubmissions.GetFormSubmissions;

/// <summary>A file attached to a submission — enough to list and to fetch it, never the bytes.</summary>
public sealed record FormAttachmentSummary(int Id, string FieldName, string FileName, long Size);
