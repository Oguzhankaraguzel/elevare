using Application.Abstraction.Services.Storage;
using Domain.Entities.Storage;
using Microsoft.AspNetCore.StaticFiles;
using SharedKernel.Concrete;

namespace Wasm.Endpoints;

/// <summary>
/// Serves uploaded media (<c>/uploads/...</c>).
/// <para>
/// This deliberately does NOT go through the static-file middleware. Blazor swaps
/// <c>WebRootFileProvider</c> for a static-web-assets manifest built at COMPILE
/// time, and a <c>PhysicalFileProvider</c> created at startup was measured not to
/// pick up files written afterwards either — so freshly uploaded media 404'd out of
/// static files, fell through to endpoint routing, and came back 401 from the
/// fallback auth policy. Reading through <see cref="IBlobStorage"/> has neither
/// problem, and has the further advantage of working unchanged when storage is
/// switched to S3, where there is no local file to serve at all.
/// </para>
/// </summary>
public static class MediaEndpoints
{
    /// <summary>
    /// Uploaded media is immutable — every file is stored under a freshly generated
    /// GUID name — so it can be cached hard and never revalidated.
    /// </summary>
    private const string ImmutableCacheControl = "public,max-age=31536000,immutable";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static WebApplication MapMediaEndpoints(this WebApplication app)
    {
        app.MapGet("/uploads/{**path}", async (
            string path,
            IBlobStorage storage,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            // "uploads" is part of the stored relative path, but the route already
            // consumed it — put it back before asking storage for the object.
            string relativePath = $"uploads/{path}";

            Result<Stream> opened = await storage.OpenReadAsync(relativePath, cancellationToken);

            if (opened.IsFailure)
            {
                // A missing object is an ordinary 404; anything else is a real storage
                // fault and must not be reported as "no such file", or an outage would
                // look to the operator like content that was never uploaded.
                return opened.Error.Code == BlobStorageErrors.NotFoundCode
                    ? Results.NotFound()
                    : Results.Problem(opened.Error.Description, statusCode: StatusCodes.Status502BadGateway);
            }

            // Unknown extensions are served as a generic binary download rather than
            // being guessed at, so a file with a misleading name cannot be coaxed into
            // rendering as HTML in the visitor's browser.
            if (!ContentTypes.TryGetContentType(relativePath, out string? contentType))
                contentType = "application/octet-stream";

            context.Response.Headers.CacheControl = ImmutableCacheControl;
            // Belt and braces against content sniffing overriding the type above.
            context.Response.Headers.XContentTypeOptions = "nosniff";

            // An SVG is the one image type that is also a document: opened directly
            // (not via <img>), a browser runs any <script> inside it — and since this
            // is served from the CMS origin, that script would run there. nosniff does
            // not help, because the type genuinely IS image/svg+xml. Sandboxing the
            // response neutralises the script without affecting <img>/<object> uses,
            // which never execute SVG script anyway, and leaves every other file type
            // (jpeg, pdf, mp4, …) untouched so nothing about normal media changes.
            if (string.Equals(contentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
                context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";

            return Results.Stream(opened.Value, contentType);
        })
        // Media is embedded in pages that anonymous visitors read, so it must not sit
        // behind the CMS's authenticated fallback policy.
        .AllowAnonymous();

        return app;
    }
}
