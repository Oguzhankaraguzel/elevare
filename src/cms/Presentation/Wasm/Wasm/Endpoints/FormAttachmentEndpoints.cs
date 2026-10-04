using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Wasm.Endpoints;

/// <summary>
/// Hands a form attachment to the person reading the submission. A plain link
/// rather than a JS-interop download: the bytes can be ten megabytes, well past
/// what a Blazor circuit message carries, and a link is what a browser knows how
/// to save. The session cookie travels with it, so the fallback policy already
/// requires a signed-in user; the permission is the same one the submissions
/// screen itself is behind.
/// <para>
/// Always <c>attachment</c>, never inline, and <c>nosniff</c>: this is a visitor's
/// file, served from the CMS's own origin, and nothing a visitor uploaded should
/// ever render as a page on it.
/// </para>
/// </summary>
public static class FormAttachmentEndpoints
{
    public static WebApplication MapFormAttachmentEndpoints(this WebApplication app)
    {
        app.MapGet("/forms/attachments/{id:int}", async (
            int id, ICmsApplicationDbContext db, IUserContext user, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!user.HasPermission(PermissionKeys.FormsViewSubmissions))
                return Results.Forbid();

            var file = await db.FormSubmissionAttachments
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new { a.FileName, a.ContentType, a.Content })
                .FirstOrDefaultAsync(cancellationToken);

            if (file is null)
                return Results.NotFound();

            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "private,no-store";
            return Results.File(file.Content, file.ContentType, fileDownloadName: file.FileName);
        });

        return app;
    }
}
