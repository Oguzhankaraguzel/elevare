using Domain.Entities.Media;
using Microsoft.AspNetCore.Components.Forms;

namespace Wasm.Components.Shared.Utilities;

public partial class MediaManager
{
    /// <summary>
    /// A real camera photo routinely lands in the 3-5 MB range, so a lower cutoff
    /// would leave the dialog with no preview at all for ordinary photographs. A
    /// much higher one would mean base64-encoding tens of megabytes into the DOM
    /// just to draw a thumbnail — real cost for nothing past "is this the right
    /// picture." Files above this are shown as an icon instead.
    /// </summary>
    private const long PreviewThresholdBytes = 8_000_000;

    /// <summary>
    /// Reads a staged image into memory for the dialog's thumbnail. Returns no bytes
    /// (and no error) for files that are not previewable — a PDF, or an image past
    /// <see cref="PreviewThresholdBytes"/>.
    /// <para>
    /// Lives here rather than in the .razor file because the analyzer suppression
    /// below has to sit on the real line: pragmas inside an @code block are written
    /// into generated code whose line mapping does not line up with the source.
    /// </para>
    /// </summary>
    private static async Task<(byte[]? Bytes, string? PreviewUrl, string? Error)> TryReadPreviewAsync(IBrowserFile file)
    {
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || file.Size > PreviewThresholdBytes)
            return (null, null, null);

        try
        {
            using var buffer = new MemoryStream();
            // S5693 reads this as an unbounded request-size limit. It is the
            // opposite: the argument IS the cap, and it is far below the upload
            // limit the server enforces separately (FileServiceOptions).
#pragma warning disable S5693
            await file.OpenReadStream(PreviewThresholdBytes).CopyToAsync(buffer);
#pragma warning restore S5693

            byte[] bytes = buffer.ToArray();
            return (bytes, $"data:{file.ContentType};base64,{Convert.ToBase64String(bytes)}", null);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TaskCanceledException)
        {
            return (null, null, ex.Message);
        }
    }

    /// <summary>
    /// Opens the upload stream for a file too large to have been buffered for a
    /// preview. Same suppression rationale as <see cref="TryReadPreviewAsync"/>.
    /// </summary>
    private Stream OpenUploadStream(IBrowserFile file)
    {
#pragma warning disable S5693
        return file.OpenReadStream(_maxUploadBytes);
#pragma warning restore S5693
    }

    private sealed record MediaFileDto(
        int Id,
        string FileName,
        string OriginalFileName,
        string FilePath,
        string MimeType,
        long FileSize,
        string? AltText,
        string? Title,
        MediaType MediaType,
        int? Width,
        int? Height,
        DateTime CreateDate,
        string? Renditions)
    {
        /// <summary>Pixel size for display, e.g. <c>1920 × 1072</c>.</summary>
        public string? DimensionLabel =>
            Width.HasValue && Height.HasValue ? $"{Width} × {Height}" : null;
    }
}
