namespace Application.Features.Commands.Forms.RecordFormSubmission;

/// <summary>
/// One file the endpoint has already checked against <c>FormAttachmentPolicy</c>:
/// the name is the cleaned one, the content type the one the bytes established.
/// </summary>
public sealed record FormAttachmentInput(string FieldName, string FileName, string ContentType, byte[] Content);
