using System.Text.Json;
using Application.Abstraction.Services;
using Application.Features.Commands.Forms.RecordFormSubmission;
using MediatR;
using Microsoft.AspNetCore.Http.Features;
using SharedKernel.Concrete;
using SharedKernel.Forms;
using WebMvc.RateLimiting;

namespace WebMvc.Endpoints;

/// <summary>
/// Receives submissions of GrapesJS-authored forms (<c>data-elevare-managed-form</c>)
/// from the small runtime script in elevare-interactions.js.
/// <para>
/// Two shapes, one route. A form without files posts JSON, as it always has. A
/// form with a file input posts <c>multipart/form-data</c>: the same fields, plus
/// the files — each admitted only under <see cref="FormAttachmentPolicy"/> (count,
/// size, and the type its own bytes declare), and refused with a reason code the
/// script turns into a sentence for the visitor. The CAPTCHA and the rate limit
/// guard both shapes alike.
/// </para>
/// </summary>
public static class FormEndpoints
{
    // Three files at the cap plus the fields, with headroom for multipart framing.
    private const long MaxMultipartBytes = FormAttachmentPolicy.MaxFiles * FormAttachmentPolicy.MaxFileBytes + 1024 * 1024;

    public static WebApplication MapFormEndpoints(this WebApplication app)
    {
        app.MapPost("/api/forms/submit", async (
            HttpContext httpContext, ISender sender, ICaptchaVerifier captchaVerifier, CancellationToken ct) =>
        {
            FormSubmitRequest? request;
            List<FormAttachmentInput> attachments = [];

            if (httpContext.Request.HasFormContentType)
            {
                // Set on the request, not as endpoint metadata: the attribute form is
                // an MVC filter, and a minimal API reading the body itself has to say
                // so through the feature before the first byte is read.
                IHttpMaxRequestBodySizeFeature? bodySize = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (bodySize is { IsReadOnly: false }) bodySize.MaxRequestBodySize = MaxMultipartBytes;

                (request, attachments, string? reason) = await ReadMultipartAsync(httpContext.Request, ct);
                if (reason is not null)
                    return Results.BadRequest(new FormSubmitResponse(false, reason));
            }
            else
            {
                request = await httpContext.Request.ReadFromJsonAsync<FormSubmitRequest>(ct);
            }

            if (request is null)
                return Results.BadRequest(new FormSubmitResponse(false));

            string? remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
            Result captcha = await captchaVerifier.VerifyAsync(request.CaptchaToken, remoteIp, ct);
            if (captcha.IsFailure)
                return Results.BadRequest(new FormSubmitResponse(false, "captcha"));

            string fieldsJson = JsonSerializer.Serialize(request.Fields ?? new Dictionary<string, string>());

            Result<int> result = await sender.Send(
                new RecordFormSubmissionCommand(request.PageId, request.FormName, fieldsJson, attachments), ct);

            return result.IsSuccess
                ? Results.Ok(new FormSubmitResponse(true))
                : Results.BadRequest(new FormSubmitResponse(false));
        })
        .RequireRateLimiting(RateLimitPolicies.FormSubmit)
        .DisableAntiforgery();

        return app;
    }

    /// <summary>
    /// The multipart shape: <c>pageId</c>, <c>formName</c>, <c>captchaToken</c> and
    /// the form's own fields as values; the files as files. Every file is read to
    /// the cap and no further, sniffed, and either kept with the type the bytes
    /// establish or refused — and one refused file refuses the submission, so a
    /// visitor is never told "sent" about a form that lost its attachment. The
    /// fields JSON records each file by name and size where its field was, so the
    /// submission reads whole in the CMS even before the attachments are opened.
    /// </summary>
    private static async Task<(FormSubmitRequest? Request, List<FormAttachmentInput> Files, string? Reason)> ReadMultipartAsync(
        HttpRequest http, CancellationToken ct)
    {
        IFormCollection form;
        try
        {
            form = await http.ReadFormAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException or IOException)
        {
            return (null, [], "too_large");
        }

        if (form.Files.Count > FormAttachmentPolicy.MaxFiles)
            return (null, [], "too_many");

        Dictionary<string, string> fields = [];
        int pageId = 0;
        string? formName = null, captchaToken = null;
        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in form)
        {
            string value = kv.Value.ToString();
            switch (kv.Key)
            {
                case "pageId": _ = int.TryParse(value, out pageId); break;
                case "formName": formName = value; break;
                case "captchaToken": captchaToken = value; break;
                default: fields[kv.Key] = value; break;
            }
        }

        List<FormAttachmentInput> files = [];
        foreach (IFormFile file in form.Files)
        {
            if (file.Length == 0) continue;
            if (file.Length > FormAttachmentPolicy.MaxFileBytes)
                return (null, [], "too_large");

            byte[] bytes;
            using (Stream stream = file.OpenReadStream())
            using (MemoryStream buffer = new())
            {
                await stream.CopyToAsync(buffer, ct);
                bytes = buffer.ToArray();
            }
            if (bytes.LongLength > FormAttachmentPolicy.MaxFileBytes)
                return (null, [], "too_large");

            FormAttachmentPolicy.Kind? kind = FormAttachmentPolicy.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, FormAttachmentPolicy.DetectHeadBytes)));
            if (kind is null)
                return (null, [], "type");

            string fieldName = file.Name.Length > 200 ? file.Name[..200] : file.Name;
            string fileName = FormAttachmentPolicy.SafeFileName(file.FileName, kind);
            files.Add(new FormAttachmentInput(fieldName, fileName, kind.ContentType, bytes));

            string summary = $"{fileName} ({FormatSize(bytes.LongLength)})";
            fields[fieldName] = fields.TryGetValue(fieldName, out string? existing) && existing.Length > 0
                ? existing + ", " + summary
                : summary;
        }

        return (new FormSubmitRequest(pageId, formName, fields, captchaToken), files, null);
    }

    private static string FormatSize(long bytes) =>
        bytes < 1024 * 1024
            ? $"{bytes / 1024.0:0.#} KB"
            : $"{bytes / (1024.0 * 1024.0):0.#} MB";
}
