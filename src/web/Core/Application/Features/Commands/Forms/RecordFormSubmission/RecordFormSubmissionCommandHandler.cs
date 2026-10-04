using Application.Abstraction.Data;
using Domain.Entities.PublicForms;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Forms;

namespace Application.Features.Commands.Forms.RecordFormSubmission;

internal sealed class RecordFormSubmissionCommandHandler(
    IAnalyticsDbContext db,
    IPublicReadDbContext read)
    : ICommandHandler<RecordFormSubmissionCommand, int>
{
    private const int MaxFormNameLength = 200;
    private const int MaxFieldsJsonLength = 20000;

    public async Task<Result<int>> Handle(RecordFormSubmissionCommand request, CancellationToken cancellationToken)
    {
        if (request.PageId <= 0)
            return Result.Failure<int>(PublicFormSubmissionErrors.InvalidPage);

        if (!HasAnyField(request.FieldsJson))
            return Result.Failure<int>(PublicFormSubmissionErrors.EmptyFields);

        if (request.FieldsJson.Length > MaxFieldsJsonLength)
            return Result.Failure<int>(PublicFormSubmissionErrors.TooLarge);

        // The page id arrives from the browser, so it is a caller-supplied value like any
        // other. Without this check a bad one reached the INSERT and the foreign key threw:
        // the endpoint answered 500 with an HTML error page instead of JSON, and anyone
        // could produce an unhandled exception on demand just by posting a made-up id.
        bool pageExists = await read.PageInfos
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.PageId, cancellationToken);

        if (!pageExists)
            return Result.Failure<int>(PublicFormSubmissionErrors.InvalidPage);

        string? formName = request.FormName?.Trim();
        if (formName is { Length: > MaxFormNameLength })
            formName = formName[..MaxFormNameLength];

        var submission = new PublicFormSubmission
        {
            PageInfoId = request.PageId,
            FormName = string.IsNullOrEmpty(formName) ? null : formName,
            FieldsJson = request.FieldsJson,
            SubmittedAtUtc = DateTime.UtcNow
        };

        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

        // The endpoint has already applied FormAttachmentPolicy (count, size, type
        // by content); the cap is re-checked here so no other caller can slip
        // past it. Written after the submission so the FK has an id to point at,
        // in the same unit of work as far as the caller can tell.
        IReadOnlyList<FormAttachmentInput> attachments = request.Attachments ?? [];
        if (attachments.Count > FormAttachmentPolicy.MaxFiles || attachments.Any(a => a.Content.LongLength > FormAttachmentPolicy.MaxFileBytes))
            return Result.Failure<int>(PublicFormSubmissionErrors.AttachmentPolicy);

        foreach (FormAttachmentInput a in attachments)
        {
            db.FormSubmissionAttachments.Add(new PublicFormSubmissionAttachment
            {
                FormSubmissionId = submission.Id,
                FieldName = a.FieldName,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Size = a.Content.LongLength,
                Content = a.Content,
            });
        }
        if (attachments.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return Result.Success(submission.Id);
    }

    /// <summary>
    /// Whether the payload carries a field at all.
    /// <para>
    /// The endpoint serialises a missing form body to <c>"{}"</c>, which is not
    /// null, empty or whitespace — so the original guard never once rejected
    /// anything and empty rows piled up in the table. Compared against the two
    /// shapes an empty object can take rather than parsed, because this runs on an
    /// anonymous endpoint and the size check has not happened yet.
    /// </para>
    /// </summary>
    private static bool HasAnyField(string? fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson))
            return false;

        ReadOnlySpan<char> trimmed = fieldsJson.AsSpan().Trim();
        return !trimmed.IsEmpty
            && !trimmed.SequenceEqual("{}")
            && !trimmed.SequenceEqual("[]");
    }
}
