using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Forms.RecordFormSubmission;

/// <summary>Appends one visitor form submission for the given page.</summary>
/// <param name="Attachments">Files the visitor attached, already validated by the endpoint; empty for the ordinary form.</param>
public sealed record RecordFormSubmissionCommand(
    int PageId, string? FormName, string FieldsJson,
    IReadOnlyList<FormAttachmentInput>? Attachments = null) : ICommand<int>;
