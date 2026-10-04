using Domain.Entities.Media;

namespace Application.Abstraction.Services.Files;

/// <summary>Read model returned after a successful file operation.</summary>
public sealed class FileResult
{
    public int Id { get; init; }

    /// <summary>Unique server-side file name (GUID-based, includes extension).</summary>
    public required string FileName { get; init; }

    /// <summary>Original name as supplied by the uploader.</summary>
    public required string OriginalFileName { get; init; }

    /// <summary>Relative URL for serving the file (e.g. <c>/uploads/images/2025/03/abc.jpg</c>).</summary>
    public required string Url { get; init; }

    /// <summary>Physical path on the server.</summary>
    public required string FilePath { get; init; }

    /// <summary>Logical folder path stored in DB (e.g. <c>uploads/images/2025/03</c>).</summary>
    public required string FolderPath { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; init; }

    public required string MimeType { get; init; }
    public MediaType MediaType { get; init; }

    public string? AltText { get; init; }
    public string? Title { get; init; }

    /// <summary>Image width in pixels; <c>null</c> for non-image types.</summary>
    public int? Width { get; init; }

    /// <summary>Image height in pixels; <c>null</c> for non-image types.</summary>
    public int? Height { get; init; }

    /// <summary>
    /// The derived copies of an image, as stored (see <c>SharedKernel.Media.ImageRenditions</c>); null otherwise.
    /// </summary>
    public string? Renditions { get; init; }

    public DateTime UploadedAt { get; init; }
}
